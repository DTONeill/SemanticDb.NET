namespace SemanticDb.Core.Configuration;

/// <summary>
/// Holds the runtime registration of a single <see cref="ISearchableEntity{T}"/> implementation.
/// </summary>
public sealed class SearchableEntityRegistration
{
    /// <summary>
    /// The name of the chunk, derived from the implementation class name.
    /// </summary>
    public required string ChunkName { get; init; }

    /// <summary>
    /// The <see cref="ISearchableEntity{T}"/> implementation type.
    /// </summary>
    public required Type ImplementationType { get; init; }

    /// <summary>
    /// The entity type being indexed.
    /// </summary>
    public required Type EntityType { get; init; }

    /// <summary>
    /// The current version of the chunk definition.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// Renders the search content for a given entity instance.
    /// </summary>
    public required Func<object, string> ToSearchContent { get; init; }

    /// <summary>
    /// Renders the prompt context for a given entity instance.
    /// </summary>
    public required Func<object, string> ToPromptContext { get; init; }

    /// <summary>
    /// Extracts the scope key from a given entity instance.
    /// </summary>
    public required Func<object, object?> GetScopeKey { get; init; }
}
