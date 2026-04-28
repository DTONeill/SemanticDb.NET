using SemanticDb.Core.Abstractions;

namespace SemanticDb.Core.Chunk;

/// <summary>
/// Represents a single semantic search result.
/// </summary>
public sealed class SemanticDbResult
{
    /// <summary>
    /// The serialized primary key of the matching entity.
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    /// The scope key value, if any.
    /// </summary>
    public string? ScopeKey { get; init; }

    /// <summary>
    /// The similarity score between the query and this chunk (0 to 1).
    /// </summary>
    public required float Score { get; init; }

    /// <summary>
    /// The prompt context text for this chunk, as rendered by <see cref="ISearchableEntity{T}.ToPromptContext"/>.
    /// Ready to pass directly to an LLM without an extra database round-trip.
    /// </summary>
    public required string PromptContext { get; init; }
}
