using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Search;
using SemanticDb.EF.SqlServer.Search;

namespace SemanticDb.EF.SqlServer.Extensions;

/// <summary>
/// Query-time strategy selection extensions for SQL Server search.
/// </summary>
public static class SemanticSearchQueryExtensions
{
    /// <summary>
    /// Selects the SQL Server native <c>VECTOR_DISTANCE</c> search strategy.
    /// Requires <c>UseSqlServer()</c> to be configured at startup.
    /// </summary>
    public static SemanticSearchQuery<TSearchableEntity> UseSqlServerVectorSearch<TSearchableEntity>(
        this SemanticSearchQuery<TSearchableEntity> query)
        where TSearchableEntity : ISearchableEntity
        => query.WithStrategy(typeof(ISqlServerVectorSearch));
}
