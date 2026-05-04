using System.ComponentModel;

namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Infrastructure marker interface for <see cref="ISearchableEntity{T,TScopeKey}"/>.
/// Used as a generic constraint for <see cref="ISemanticDbService.SearchAsync{TSearchableEntity}"/>.
/// Do not implement this interface directly — implement <see cref="ISearchableEntity{T,TScopeKey}"/> instead.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISearchableEntity
{
}

/// <summary>
/// Infrastructure interface that carries the scope key type for the <see cref="ISemanticDbService.SearchAsync{TSearchableEntity,TScopeKey}"/> constraint.
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
/// <typeparam name="T">The entity type to index.</typeparam>
/// <typeparam name="TScopeKey">
/// The type of the scope key used to partition chunks (e.g. <see cref="int"/> for a patient ID,
/// <see cref="Guid"/> for a tenant ID). Use <see cref="object"/> if no scoping is needed.
/// </typeparam>
public interface ISearchableEntity<T, TScopeKey> : ISearchableEntity<TScopeKey> where T : class
{
    bool IsDeleted(T entity) => false;

    /// <summary>
    /// Returns the text content to embed and index for semantic search.
    /// </summary>
    string ToSearchContent(T entity);

    /// <summary>
    /// Returns the text sent to the LLM as context when this entity matches a query.
    /// Defaults to <see cref="ToSearchContent"/> if not overridden.
    /// </summary>
    string ToPromptContext(T entity) => ToSearchContent(entity);

    /// <summary>
    /// Returns the scope key used to partition chunks (e.g. tenant ID, patient ID).
    /// Return <see langword="null"/> if no scoping is needed.
    /// <para>
    /// The return type is <see cref="object"/>? so any value type can be returned without boxing issues.
    /// <typeparamref name="TScopeKey"/> is used exclusively to enforce the correct type at
    /// <see cref="ISemanticDbService.SearchAsync{TSearchableEntity, TScopeKey}"/> call sites.
    /// </para>
    /// </summary>
    object? GetScopeKey(T entity) => null;

    /// <summary>
    /// The version of this chunk definition.
    /// Increment this value when <see cref="ToSearchContent"/> changes
    /// to trigger automatic re-indexing of all entities of this type.
    /// </summary>
    int Version => 1;
}
