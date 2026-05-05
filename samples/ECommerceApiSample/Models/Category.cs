namespace ECommerceApiSample.Models;

/// <summary>
/// Represents a top-level product category in the catalog.
/// </summary>
public sealed class Category
{
    /// <summary>Auto-incremented primary key.</summary>
    public int Id { get; set; }

    /// <summary>Short display name (e.g. "Electronics", "Books").</summary>
    public required string Name { get; set; }

    /// <summary>Full prose description used for search context.</summary>
    public required string Description { get; set; }

    /// <summary>Navigation: all products in this category.</summary>
    public List<Product> Products { get; set; } = [];
}
