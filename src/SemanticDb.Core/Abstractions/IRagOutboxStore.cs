using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;

namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Defines the contract for reading and writing outbox entries that drive the indexing pipeline.
/// </summary>
public interface IRagOutboxStore
{
    /// <summary>
    /// Resets stale processing entries back to pending so they can be retried.
    /// An entry is considered stale if it has been in a processing state longer than <paramref name="staleTimeout"/>.
    /// </summary>
    public Task SetStaleEntriesToPending(TimeSpan staleTimeout, CancellationToken cancellationToken);

    /// <summary>
    /// Returns outbox entries matching the given criteria.
    /// </summary>
    Task<List<RagOutboxEntry>> ListAsync(RagSearchCriteria criteria, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims up to <paramref name="batchSize"/> pending entries for the given processor instance.
    /// </summary>
    Task ClaimBatchAsync(string instanceId, int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Persists mutations applied to the given entries (e.g. status updates after processing).
    /// </summary>
    Task UpsertBatchAsync(IReadOnlyList<RagOutboxEntry> entries, string instanceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Permanently removes the outbox entries with the given IDs.
    /// </summary>
    Task DeleteBatchAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues a full re-index of all entities of the given type by inserting pending outbox entries in batches.
    /// </summary>
    Task EnqueueReindexAsync(
        string chunkName,
        Type entityClrType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a single entity for re-indexing. If an unclaimed outbox entry already exists for the
    /// same chunk and entity it is reset to <c>Pending</c> rather than duplicated.
    /// </summary>
    Task EnqueueEntityReindexAsync(
        string chunkName,
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of outbox entries in the given status.
    /// Used by health checks to surface permanently-failed entries.
    /// </summary>
    Task<int> CountByStatusAsync(RagOutboxStatus status, CancellationToken cancellationToken = default);
}
