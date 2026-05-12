using SemanticDb.Core.Chunk;

namespace SemanticDb.Core.Search;

/// <summary>
/// Defines a search strategy that can be selected at query time via
/// <see cref="SemanticSearchQuery{TSearchableEntity}.WithStrategy"/>.
/// </summary>
/// <remarks>
/// Implement this interface and register via
/// <see cref="SemanticDb.Core.Configuration.SearchStrategyRegistry.Register"/> to add
/// a custom strategy. Provide a corresponding extension method on
/// <see cref="SemanticSearchQuery{TSearchableEntity}"/> to expose it to callers.
/// </remarks>
public interface ISearchStrategy
{
    /// <summary>
    /// Executes the search described by <paramref name="context"/> and returns results ordered by relevance.
    /// </summary>
    Task<IReadOnlyList<SemanticDbResult>> ExecuteAsync(SearchExecutionContext context, CancellationToken ct);
}
