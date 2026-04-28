using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;

namespace SemanticDb.EF.Extensions;

/// <summary>
/// Extension methods for configuring the semantic search schema in a <see cref="ModelBuilder"/>.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Registers the semantic search tables (<c>RagChunks</c> and <c>RagOutbox</c>)
    /// into the user's <see cref="ModelBuilder"/>.
    /// </summary>
    /// <param name="builder">The model builder.</param>
    /// <returns>The same model builder for chaining.</returns>
    public static ModelBuilder ApplySemanticDbConfiguration(this ModelBuilder builder)
    {
        builder.Entity<RagIndexState>(entity =>
        {
            entity.ToTable("RagIndexState");
            entity.HasKey(x => x.ChunkName);
            entity.Property(x => x.ChunkName).IsRequired().HasMaxLength(128);
            entity.Property(x => x.CompositeVersion).IsRequired();
            entity.Property(x => x.LastIndexedAt).IsRequired();
        });

        builder.Entity<RagOutboxEntry>(entity =>
        {
            entity.ToTable("RagOutbox");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityType).IsRequired().HasMaxLength(512);
            entity.Property(x => x.EntityId).IsRequired().HasMaxLength(256);
            entity.Property(x => x.ChunkName).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.ProcessedAt);
            entity.Property(x => x.Error);
            entity.Property(x => x.RetryCount).IsRequired();
            entity.Property(x => x.NextRetryAt);
            entity.Property(x => x.ClaimedBy).HasMaxLength(256);
            entity.Property(x => x.ClaimedAt);
            entity.HasIndex(x => new { x.Status, x.NextRetryAt });
            entity.HasIndex(x => new { x.ClaimedBy, x.Status });
        });

        builder.Entity<RagChunk>(entity =>
        {
            entity.ToTable("RagChunks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChunkName).IsRequired().HasMaxLength(128);
            entity.Property(x => x.EntityId).IsRequired().HasMaxLength(256);
            entity.Property(x => x.ScopeKey).HasMaxLength(256);
            entity.Property(x => x.PromptContext).IsRequired();
            entity.Property(x => x.Embedding).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasIndex(x => new { x.ChunkName, x.EntityId }).IsUnique();
            entity.HasIndex(x => new { x.ChunkName, x.ScopeKey });
        });

        return builder;
    }
}
