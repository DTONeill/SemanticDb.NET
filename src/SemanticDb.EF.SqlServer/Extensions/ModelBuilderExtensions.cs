using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Models;

namespace SemanticDb.EF.SqlServer.Extensions;

/// <summary>
/// Extension methods for configuring SQL Server vector search on the model.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Configures the <c>RagChunks.Embedding</c> column as a SQL Server native
    /// <c>VECTOR(dimensions)</c> type.
    /// Call this inside <c>OnModelCreating</c> when using SQL Server vector search.
    /// </summary>
    public static ModelBuilder UseSqlServerVectorSearch(
        this ModelBuilder builder,
        int dimensions = 1536)
    {
        builder.Entity<RagChunk>()
            .Property(x => x.Embedding)
            .HasColumnType($"VECTOR({dimensions})");

        return builder;
    }
}
