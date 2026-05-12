using System.ComponentModel;

namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Infrastructure marker interface for <see cref="ISearchableEntity{T,TScopeKey}"/>.
/// Used as a generic constraint for <see cref="ISemanticSearcher{TSearchableEntity}"/>.
/// Do not implement this interface directly — implement <see cref="ISearchableEntity{T,TScopeKey}"/> instead.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISearchableEntity
{
}

/// <summary>
/// Infrastructure interface that carries the scope key type for compile-time scope key validation
/// via <see cref="SemanticDb.Core.Search.SemanticSearchQuery{TSearchableEntity}.WithScope"/>.
/// Do not implement this interface directly — implement <see cref="ISearchableEntity{T,TScopeKey}"/> instead.
/// </summary>
/// <typeparam name="TScopeKey">The type of the scope key (e.g. <see cref="int"/>, <see cref="Guid"/>).</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISearchableEntity<TScopeKey> : ISearchableEntity
{
}

/// <summary>
/// Defines how an entity is indexed and contextualized for semantic search.
/// Implement this interface in a dedicated class — not on the entity itself.
/// </summary>
/// <remarks>
/// Implementations are discovered automatically via assembly scanning in
/// <c>AddSemanticDb(params Assembly[])</c> and registered as
/// <see cref="ISemanticSearcher{TSearchableEntity}"/> in the DI container.
/// Inject <see cref="ISemanticSearcher{TSearchableEntity}"/> to query the index.
/// </remarks>
/// <typeparam name="T">The entity type to index.</typeparam>
/// <typeparam name="TScopeKey">
/// The type of the scope key used to partition chunks (e.g. <see cref="int"/> for a tenant ID,
/// <see cref="Guid"/> for a user ID). Use <see cref="object"/> if no scoping is needed.
/// </typeparam>
public interface ISearchableEntity<T, TScopeKey> : ISearchableEntity<TScopeKey> where T : class
{
    /// <summary>
    /// Returns <see langword="true"/> if the entity should be removed from the search index.
    /// Override to support soft deletes; defaults to <see langword="false"/>.
    /// </summary>
    bool IsDeleted(T entity) => false;

    /// <summary>
    /// Returns the text content to embed and index for semantic search.
    /// </summary>
    string ToSearchContent(T entity);

    /// <summary>
    /// Returns the text passed to the LLM as context when this entity matches a query.
    /// Defaults to <see cref="ToSearchContent"/> if not overridden.
    /// </summary>
    string ToPromptContext(T entity) => ToSearchContent(entity);

    /// <summary>
    /// Returns the scope key used to partition chunks (e.g. tenant ID, user ID).
    /// Return <see langword="null"/> if no scoping is needed.
    /// <para>
    /// The return type is <see cref="object"/>? so any value type can be returned without boxing.
    /// <typeparamref name="TScopeKey"/> enforces the correct key type at
    /// <see cref="SemanticDb.Core.Search.SemanticSearchQuery{TSearchableEntity}.WithScope"/> call sites.
    /// </para>
    /// </summary>
    object? GetScopeKey(T entity) => null;

    /// <summary>
    /// The version of this chunk definition.
    /// Increment when <see cref="ToSearchContent"/> changes to trigger automatic re-indexing.
    /// </summary>
    int Version => 1;
}
