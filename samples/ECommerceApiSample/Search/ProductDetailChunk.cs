using ECommerceApiSample.Models;
using SemanticDb.Core.Abstractions;

namespace ECommerceApiSample.Search;

/// <summary>
/// Indexes detailed product information for global (unscoped) semantic search.
/// Demonstrates two things:
/// <list type="bullet">
///   <item>A second chunk definition on the same <see cref="Product"/> entity type.</item>
///   <item>A <see cref="ToPromptContext"/> that is richer than <see cref="ToSearchContent"/>.</item>
/// </list>
/// Archived products are excluded from the index via <see cref="IsDeleted"/>.
/// </summary>
public sealed class ProductDetailChunk : ISearchableEntity<Product, string>
{
    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public bool IsDeleted(Product entity) => entity.IsArchived;

    /// <summary>
    /// Compact text embedded into the vector store.
    /// Shorter strings reduce noise in cosine similarity calculations.
    /// </summary>
    public string ToSearchContent(Product entity) =>
        $"{entity.Name} — {entity.Description}";

    /// <summary>
    /// Rich markdown returned to the LLM in RAG responses.
    /// Includes price and explicit field labels so the model can cite specifics.
    /// Intentionally differs from <see cref="ToSearchContent"/> to show the split.
    /// </summary>
    public string ToPromptContext(Product entity) =>
        $"## {entity.Name}\n" +
        $"**Price:** ${entity.Price:F2}\n\n" +
        $"{entity.Description}";

    // No scope key — supports global search across all categories.
    /// <inheritdoc />
    public object? GetScopeKey(Product entity) => null;
}
