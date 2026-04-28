using SemanticDb.Core.Outbox;

namespace SemanticDb.Core.Models;

/// <summary>
/// Represents a pending indexing operation stored in the outbox table.
/// Written in the same transaction as the entity change, processed asynchronously.
/// </summary>
public sealed class RagOutboxEntry
{
    /// <summary>
    /// The unique identifier of this outbox entry.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The full type name of the entity that was modified.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// The serialized primary key of the entity.
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    /// The name of the chunk definition to apply.
    /// </summary>
    public required string ChunkName { get; init; }

    /// <summary>
    /// The current processing status of this entry.
    /// </summary>
    public RagOutboxStatus Status { get; set; } = RagOutboxStatus.Pending;

    /// <summary>
    /// The UTC timestamp when this entry was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The UTC timestamp of the last processing attempt.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// The error message if processing failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The number of times processing has been attempted.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// The earliest UTC time at which this entry may be retried.
    /// Null means the entry is eligible immediately.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// The instance ID that claimed this entry for processing.
    /// </summary>
    public string? ClaimedBy { get; set; }

    /// <summary>
    /// The UTC timestamp when this entry was claimed.
    /// </summary>
    public DateTime? ClaimedAt { get; set; }
}
