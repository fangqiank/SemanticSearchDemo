using System.Text.Json.Serialization;

namespace SemanticSearchDemo.Api.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelName;
        private readonly string _baseUrl;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<EmbeddingService> logger)
        {
            _httpClient = httpClient;
            _modelName = configuration.GetValue<string>("Ollama:ModelName") ?? "qwen3-embedding:0.6b";
            _baseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434";
            _logger = logger;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new
                {
                    model = _modelName,
                    prompt = text
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_baseUrl}/api/embeddings",
                    request,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);

                if (result?.Embedding == null || result.Embedding.Length == 0)
                {
                    throw new InvalidOperationException("Failed to generate embedding: empty response");
                }

                _logger.LogDebug("Generated embedding with {Dimension} dimensions", result.Embedding.Length);
                return result.Embedding;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text: {Text}", text[..Math.Min(100, text.Length)]);
                throw;
            }
        }

        public async Task<List<float[]>> GenerateEmbeddingsBatchAsync(List<string> texts, CancellationToken cancellationToken = default)
        {
            var tasks = texts.Select(t => GenerateEmbeddingAsync(t, cancellationToken));
            return (await Task.WhenAll(tasks)).ToList();
        }
    }

    public class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
