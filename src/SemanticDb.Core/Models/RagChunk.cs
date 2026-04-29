using SemanticDb.Core.Abstractions;

namespace SemanticDb.Core.Models;

/// <summary>
/// Represents a stored semantic search chunk with its embedding vector.
/// </summary>
public sealed class RagChunk
{
    /// <summary>
    /// The unique identifier of this chunk.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The name of the chunk definition that produced this chunk.
    /// </summary>
    public required string ChunkName { get; init; }

    /// <summary>
    /// The serialized primary key of the source entity.
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    /// The scope key value, if any.
    /// </summary>
    public string? ScopeKey { get; set; }

    /// <summary>
    /// The rendered prompt context text for this chunk, as returned by <see cref="ISearchableEntity{T,TScopeKey}.ToPromptContext"/>.
    /// </summary>
    public required string PromptContext { get; set; }

    /// <summary>
    /// The embedding vector produced by the embedding model.
    /// </summary>
    public required float[] Embedding { get; set; }

    /// <summary>
    /// The UTC timestamp when this chunk was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
