namespace SemanticDb.Core.Search;

/// <summary>
/// Carries the resolved query parameters passed to an <see cref="ISearchStrategy"/>.
/// Strategies obtain their service dependencies (embedding generators, vector stores, etc.)
/// via DI constructor injection rather than through this context.
/// </summary>
public sealed record SearchExecutionContext
{
    /// <summary>The natural language query text to search with.</summary>
    public required string QueryText { get; init; }

    /// <summary>The chunk name identifying which index to search.</summary>
    public required string ChunkName { get; init; }

    /// <summary>The scope key to filter results by, or <see langword="null"/> for all scopes.</summary>
    public string? ScopeKey { get; init; }

    /// <summary>The maximum number of results to return.</summary>
    public required int TopK { get; init; }
}
