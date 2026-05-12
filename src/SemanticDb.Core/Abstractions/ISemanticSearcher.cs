using SemanticDb.Core.Search;

namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Provides typed semantic search for a specific <typeparamref name="TSearchableEntity"/> implementation.
/// Inject this interface directly rather than using the generic overloads on <see cref="ISemanticDbService"/>.
/// </summary>
/// <typeparam name="TSearchableEntity">
/// The <see cref="ISearchableEntity{T,TScopeKey}"/> implementation that defines
/// how entities of this type are indexed and searched.
/// </typeparam>
public interface ISemanticSearcher<TSearchableEntity>
    where TSearchableEntity : ISearchableEntity
{
    /// <summary>
    /// Begins a fluent search query against the chunks indexed for <typeparamref name="TSearchableEntity"/>.
    /// Call <see cref="SemanticSearchQuery{TSearchableEntity}.ToListAsync"/> to execute.
    /// </summary>
    /// <param name="text">The natural language query text to embed and search with.</param>
    SemanticSearchQuery<TSearchableEntity> Query(string text);
}
