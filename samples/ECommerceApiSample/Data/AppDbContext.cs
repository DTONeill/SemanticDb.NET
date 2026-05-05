using ECommerceApiSample.Models;
using Microsoft.EntityFrameworkCore;
using SemanticDb.EF.Extensions;

namespace ECommerceApiSample.Data;

/// <summary>
/// Application DbContext for the e-commerce product catalog sample.
/// Uses EF Core in-memory vector search via SemanticDb.EF (no SQL Server required).
/// </summary>
public sealed class AppDbContext : DbContext
{
    /// <inheritdoc />
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>All product categories.</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>All products, including archived ones.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>All product reviews, including unapproved ones.</summary>
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SemanticDb infrastructure tables: RagChunks, RagOutbox, RagIndexState
        modelBuilder.ApplySemanticDbConfiguration();

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(128);
            entity.Property(c => c.Description).IsRequired().HasMaxLength(1024);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(256);
            entity.Property(p => p.Description).IsRequired().HasMaxLength(4096);
            entity.Property(p => p.Price).HasColumnType("TEXT"); // SQLite stores decimals as TEXT
            entity.Property(p => p.IsArchived).IsRequired();

            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.AuthorName).IsRequired().HasMaxLength(128);
            entity.Property(r => r.Content).IsRequired().HasMaxLength(4096);
            entity.Property(r => r.IsApproved).IsRequired();

            entity.HasOne(r => r.Product)
                  .WithMany(p => p.Reviews)
                  .HasForeignKey(r => r.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
