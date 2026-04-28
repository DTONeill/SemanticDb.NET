using SemanticDb.Core.Chunk;

namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Defines the contract for performing semantic search queries.
/// </summary>
public interface ISemanticDbService
{
    /// <summary>
    /// Searches for chunks semantically similar to the given query using the specified
    /// <see cref="ISearchableEntity{T}"/> implementation as the chunk definition.
    /// </summary>
    /// <typeparam name="TSearchableEntity">The registered <see cref="ISearchableEntity{T}"/> implementation to search against.</typeparam>
    /// <param name="query">The natural language query.</param>
    /// <param name="scopeKey">An optional scope key to restrict results.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of matching results ordered by relevance.</returns>
    Task<IReadOnlyList<SemanticDbResult>> SearchAsync<TSearchableEntity>(
        string query,
        object? scopeKey = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
        where TSearchableEntity : ISearchableEntity;
}
