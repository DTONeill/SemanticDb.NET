namespace ECommerceApiSample.Models;

/// <summary>
/// Represents a product in the e-commerce catalog.
/// Soft-deleted via <see cref="IsArchived"/>; archived products are excluded from the search index.
/// </summary>
public sealed class Product
{
    /// <summary>Auto-incremented primary key.</summary>
    public int Id { get; set; }

    /// <summary>Display name of the product.</summary>
    public required string Name { get; set; }

    /// <summary>Long-form description used for semantic indexing.</summary>
    public required string Description { get; set; }

    /// <summary>Retail price in USD.</summary>
    public decimal Price { get; set; }

    /// <summary>Foreign key referencing <see cref="Category"/>.</summary>
    public int CategoryId { get; set; }

    /// <summary>Navigation property to the owning category.</summary>
    public Category Category { get; set; } = null!;

    /// <summary>
    /// When true, the product is excluded from the semantic search index.
    /// Implements soft-delete for <see cref="Search.ProductsByCategoryChunk"/>
    /// and <see cref="Search.ProductDetailChunk"/>.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Navigation: all reviews for this product.</summary>
    public List<ProductReview> Reviews { get; set; } = [];
}
