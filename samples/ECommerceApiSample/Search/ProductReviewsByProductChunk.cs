using ECommerceApiSample.Models;
using SemanticDb.Core.Abstractions;

namespace ECommerceApiSample.Search;

/// <summary>
/// Indexes customer reviews grouped by product for scoped semantic search.
/// Only approved reviews are indexed; unapproved reviews are treated as soft-deleted.
/// ScopeKey = ProductId so callers can narrow results to one product.
/// </summary>
public sealed class ProductReviewsByProductChunk : ISearchableEntity<ProductReview, string>
{
    /// <inheritdoc />
    public int Version => 1;

    /// <summary>
    /// Treats unapproved reviews as soft-deleted so they are removed from the index
    /// when <see cref="ProductReview.IsApproved"/> transitions from true to false.
    /// </summary>
    public bool IsDeleted(ProductReview entity) => !entity.IsApproved;

    /// <inheritdoc />
    public string ToSearchContent(ProductReview entity) =>
        $"review by {entity.AuthorName} — rating {entity.Rating}/5: {entity.Content}";

    /// <summary>
    /// Adds the numeric rating in bold for LLM context.
    /// </summary>
    public string ToPromptContext(ProductReview entity) =>
        $"**{entity.AuthorName}** rated this product **{entity.Rating}/5**.\n{entity.Content}";

    /// <inheritdoc />
    public object? GetScopeKey(ProductReview entity) => entity.ProductId.ToString();
}
