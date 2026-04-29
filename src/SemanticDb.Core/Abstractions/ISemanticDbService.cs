using SemanticDb.Core.Chunk;

namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Defines the contract for performing semantic search queries.
/// </summary>
public interface ISemanticDbService
{
    /// <summary>
    /// Searches for chunks semantically similar to the given query using the specified
    /// <see cref="ISearchableEntity{T,TScopeKey}"/> implementation as the chunk definition.
    /// Results are not filtered by scope.
    /// </summary>
    /// <typeparam name="TSearchableEntity">The registered <see cref="ISearchableEntity{T,TScopeKey}"/> implementation to search against.</typeparam>
    /// <param name="query">The natural language query.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of matching results ordered by relevance.</returns>
    Task<IReadOnlyList<SemanticDbResult>> SearchAsync<TSearchableEntity>(
        string query,
        int? limit = null,
        CancellationToken cancellationToken = default)
        where TSearchableEntity : ISearchableEntity;

    /// <summary>
    /// Searches for chunks semantically similar to the given query, filtered to those
    /// whose scope key matches <paramref name="scopeKey"/>.
    /// The compiler enforces that <typeparamref name="TScopeKey"/> matches the scope key type
    /// declared on <typeparamref name="TSearchableEntity"/>.
    /// </summary>
    /// <typeparam name="TSearchableEntity">The registered <see cref="ISearchableEntity{T,TScopeKey}"/> implementation to search against.</typeparam>
    /// <typeparam name="TScopeKey">The scope key type declared on <typeparamref name="TSearchableEntity"/>.</typeparam>
    /// <param name="query">The natural language query.</param>
    /// <param name="scopeKey">The scope key value to filter results by.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of matching results ordered by relevance.</returns>
    Task<IReadOnlyList<SemanticDbResult>> SearchAsync<TSearchableEntity, TScopeKey>(
        string query,
        TScopeKey scopeKey,
        int? limit = null,
        CancellationToken cancellationToken = default)
        where TSearchableEntity : ISearchableEntity<TScopeKey>;
}
