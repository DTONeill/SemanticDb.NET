using ECommerceApiSample.Models;
using SemanticDb.Core.Abstractions;

namespace ECommerceApiSample.Search;

/// <summary>
/// Indexes products grouped by category for scoped semantic search.
/// Archived products are excluded from the index via <see cref="IsDeleted"/>.
/// ScopeKey = CategoryId so callers can narrow results to one category.
/// </summary>
public sealed class ProductsByCategoryChunk : ISearchableEntity<Product, string>
{
    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public bool IsDeleted(Product entity) => entity.IsArchived;

    /// <inheritdoc />
    public string ToSearchContent(Product entity) =>
        $"product: {entity.Name}. " +
        $"description: {entity.Description}. " +
        $"price: ${entity.Price:F2}.";

    /// <inheritdoc />
    public string ToPromptContext(Product entity) => ToSearchContent(entity);

    /// <inheritdoc />
    public object? GetScopeKey(Product entity) => entity.CategoryId.ToString();
}
