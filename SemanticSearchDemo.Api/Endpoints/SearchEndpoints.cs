using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SemanticSearchDemo.Api.Model;
using SemanticSearchDemo.Api.Services;

namespace SemanticSearchDemo.Api.Endpoints
{
    public static class SearchEndpoints
    {
        public static void MapSearchEndpoints(this WebApplication app)
        {
            var searchGroup = app.MapGroup("/api/search");

            // 初始化数据库
            searchGroup.MapPost("/initialize", async (SearchService searchService, CancellationToken ct) =>
            {
                await searchService.InitializeDatabaseAsync(ct);
                return Results.Ok(new { message = "Database initialized successfully" });
            });

            // 插入单篇文章
            searchGroup.MapPost("/articles", async (CreateArticleRequest request, SearchService searchService, CancellationToken ct) =>
            {
                var validation = Validate(request);
                if (validation is not null) return validation;

                await searchService.InsertArticleAsync(request, ct);
                return Results.Created($"/api/search/articles", new { message = "Article created successfully" });
            });

            // 批量插入文章
            searchGroup.MapPost("/articles/batch", async (List<CreateArticleRequest> requests, SearchService searchService, CancellationToken ct) =>
            {
                if (requests.Count == 0)
                    return Results.BadRequest(new { error = "Request list cannot be empty" });

                if (requests.Count > 100)
                    return Results.BadRequest(new { error = "Batch size cannot exceed 100" });

                foreach (var req in requests)
                {
                    var validation = Validate(req);
                    if (validation is not null) return validation;
                }

                await searchService.InsertArticlesBatchAsync(requests, ct);
                return Results.Created("/api/search/articles", new { message = $"{requests.Count} articles created successfully" });
            });

            // 获取所有文章
            searchGroup.MapGet("/articles", async (SearchService searchService, CancellationToken ct) =>
            {
                var articles = await searchService.GetAllArticlesAsync(ct);
                return Results.Ok(articles);
            });

            // 语义搜索
            searchGroup.MapPost("/semantic", async (SearchRequest request, SearchService searchService, CancellationToken ct) =>
            {
                var validation = Validate(request);
                if (validation is not null) return validation;

                var results = await searchService.SearchSimilarAsync(request, ct);
                return Results.Ok(new
                {
                    query = request.Query,
                    totalResults = results.Count,
                    results
                });
            });

            // 快速搜索（GET 方式）
            searchGroup.MapGet("/quick", async (string? q, int? k, SearchService searchService, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(q))
                    return Results.BadRequest(new { error = "Query parameter 'q' is required" });

                var request = new SearchRequest
                {
                    Query = q,
                    TopK = k ?? 5
                };

                if (request.TopK < 1 || request.TopK > 100)
                    return Results.BadRequest(new { error = "k must be between 1 and 100" });

                var results = await searchService.SearchSimilarAsync(request, ct);
                return Results.Ok(new
                {
                    query = q,
                    totalResults = results.Count,
                    results
                });
            });
        }

        private static IResult? Validate(object obj)
        {
            var context = new ValidationContext(obj);
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(obj, context, results, validateAllProperties: true))
            {
                var errors = results.Select(r => r.ErrorMessage).ToArray();
                return Results.BadRequest(new { errors });
            }
            return null;
        }
    }
}
