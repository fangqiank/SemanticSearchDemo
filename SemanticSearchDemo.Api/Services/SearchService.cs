using Dapper;
using Npgsql;
using Pgvector;
using SemanticSearchDemo.Api.Model;

namespace SemanticSearchDemo.Api.Services
{
    public class SearchService
    {
        private readonly string _connectionString;
        private readonly EmbeddingService _embeddingService;
        private readonly ILogger<SearchService> _logger;
        private readonly int _vectorDimensions;

        public SearchService(
            IConfiguration configuration,
            EmbeddingService embeddingService,
            ILogger<SearchService> logger
            )
        {
            _connectionString = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("Postgres connection string not found");
            _embeddingService = embeddingService;
            _logger = logger;
            _vectorDimensions = configuration.GetValue<int>("Ollama:Dimensions", 1024);
        }

        public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new Npgsql.NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 启用 pgvector 扩展
            await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS vector");

            // 创建表
            await connection.ExecuteAsync($@"
                CREATE TABLE IF NOT EXISTS blog_articles (
                    id SERIAL PRIMARY KEY,
                    url TEXT NOT NULL UNIQUE,
                    title TEXT NOT NULL,
                    content TEXT NOT NULL,
                    embedding vector({_vectorDimensions}),
                    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                )"
            );

            // 创建 HNSW 索引
            await connection.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS blog_articles_embedding_idx
                ON blog_articles
                USING hnsw (embedding vector_cosine_ops)
                WITH (m = 16, ef_construction = 200)"
            );

            _logger.LogInformation("Database initialized successfully");
        }

        public async Task InsertArticleAsync(CreateArticleRequest request, CancellationToken cancellationToken = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 生成 embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(request.Content, cancellationToken);

            var vector = new Vector(embedding);

            // 插入文章
            await connection.ExecuteAsync(@"
                INSERT INTO blog_articles (url, title, content, embedding)
                VALUES (@Url, @Title, @Content, @Embedding)
                ON CONFLICT (url) DO UPDATE SET
                    title = EXCLUDED.title,
                    content = EXCLUDED.content,
                    embedding = EXCLUDED.embedding",
            new
            {
                Url = request.Url,
                Title = request.Title,
                Content = request.Content,
                Embedding = vector
            });


            _logger.LogInformation("Article inserted successfully: {Url}", request.Url);
        }

        public async Task InsertArticlesBatchAsync(List<CreateArticleRequest> articles, CancellationToken cancellationToken = default)
        {
            // 并发生成所有 embedding
            var embeddingTasks = articles.Select(a => _embeddingService.GenerateEmbeddingAsync(a.Content, cancellationToken));
            var embeddings = await Task.WhenAll(embeddingTasks);

            // 使用单个连接批量插入
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var tx = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                for (int i = 0; i < articles.Count; i++)
                {
                    var vector = new Vector(embeddings[i]);
                    await connection.ExecuteAsync(@"
                        INSERT INTO blog_articles (url, title, content, embedding)
                        VALUES (@Url, @Title, @Content, @Embedding)
                        ON CONFLICT (url) DO UPDATE SET
                            title = EXCLUDED.title,
                            content = EXCLUDED.content,
                            embedding = EXCLUDED.embedding",
                        new
                        {
                            Url = articles[i].Url,
                            Title = articles[i].Title,
                            Content = articles[i].Content,
                            Embedding = vector
                        });
                }

                await tx.CommitAsync(cancellationToken);
                _logger.LogInformation("Batch inserted {Count} articles", articles.Count);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<List<SearchResult>> SearchSimilarAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 生成查询向量
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);
            var queryVector = new Vector(queryEmbedding);

            var results = await connection.QueryAsync<SearchResult>(@"
                SELECT id, url, title, content, 1 - (embedding <=> @QueryVector) AS similarity
                FROM blog_articles
                WHERE embedding IS NOT NULL
                ORDER BY embedding <=> @QueryVector
                LIMIT @TopK",
                new
                {
                    QueryVector = queryVector,
                    TopK = request.TopK
                }
            );


            var searchResults = results.ToList();

            // 过滤最小相似度
            if (request.MinSimilarity.HasValue)
            {
                searchResults = searchResults
                    .Where(r => r.Similarity >= request.MinSimilarity.Value)
                    .ToList();
            }

            _logger.LogInformation(
               "Search completed for query '{Query}': found {Count} results",
               request.Query, searchResults.Count);

            return searchResults;
        }

        public async Task<List<BlogArticle>> GetAllArticlesAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var articles = await connection.QueryAsync<BlogArticle>(
           "SELECT id, url, title, content, created_at, updated_at FROM blog_articles ORDER BY created_at DESC");

            return articles.ToList();
        }
    }
}
