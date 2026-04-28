namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Non-generic marker interface for <see cref="ISearchableEntity{T}"/>.
/// Used as a generic constraint for <see cref="ISemanticDbService.SearchAsync{TSearchableEntity}"/>.
/// </summary>
public interface ISearchableEntity { }

/// <summary>
/// Defines how an entity is indexed and contextualized for semantic search.
/// Implement this interface in a dedicated class — not on the entity itself.
/// </summary>
/// <typeparam name="T">The entity type to index.</typeparam>
public interface ISearchableEntity<T> : ISearchableEntity where T : class
{
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
    /// Return null if no scoping is needed.
    /// </summary>
    object? GetScopeKey(T entity) => null;

    /// <summary>
    /// The version of this chunk definition.
    /// Increment this value when <see cref="ToSearchContent"/> changes
    /// to trigger automatic re-indexing of all entities of this type.
    /// </summary>
    int Version => 1;
}
