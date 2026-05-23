using System.ComponentModel.DataAnnotations;
using Pgvector;

namespace SemanticSearchDemo.Api.Model
{
    public class BlogArticle
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Vector? Embedding { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateArticleRequest
    {
        [Required(ErrorMessage = "Url is required")]
        [Url(ErrorMessage = "Invalid URL format")]
        public string Url { get; set; } = string.Empty;

        [Required(ErrorMessage = "Title is required")]
        [StringLength(500, ErrorMessage = "Title must be under 500 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content is required")]
        [StringLength(50000, ErrorMessage = "Content must be under 50000 characters")]
        public string Content { get; set; } = string.Empty;
    }

    public class SearchRequest
    {
        [Required(ErrorMessage = "Query is required")]
        public string Query { get; set; } = string.Empty;

        [Range(1, 100, ErrorMessage = "TopK must be between 1 and 100")]
        public int TopK { get; set; } = 5;

        [Range(0.0, 1.0, ErrorMessage = "MinSimilarity must be between 0 and 1")]
        public double? MinSimilarity { get; set; }
    }

    public class SearchResult
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Similarity { get; set; }
    }
}
