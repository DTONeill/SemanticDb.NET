namespace ECommerceApiSample.Models;

/// <summary>
/// Represents a customer review for a product.
/// Only approved reviews are included in the semantic search index.
/// Unapproved reviews act as soft-deleted entries via <see cref="IsApproved"/>.
/// </summary>
public sealed class ProductReview
{
    /// <summary>Auto-incremented primary key.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key referencing <see cref="Product"/>.</summary>
    public int ProductId { get; set; }

    /// <summary>Navigation property to the reviewed product.</summary>
    public Product Product { get; set; } = null!;

    /// <summary>Display name of the review author.</summary>
    public required string AuthorName { get; set; }

    /// <summary>Numeric rating from 1 (worst) to 5 (best).</summary>
    public int Rating { get; set; }

    /// <summary>Free-text review body, used for semantic indexing.</summary>
    public required string Content { get; set; }

    /// <summary>
    /// When false, this review has not been approved and is excluded from the
    /// semantic search index (treated as soft-deleted).
    /// </summary>
    public bool IsApproved { get; set; }
}
