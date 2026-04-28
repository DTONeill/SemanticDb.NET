using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;

namespace SemanticDb.Core.Abstractions;

public interface IRagOutboxStore
{
    public Task SetStaleEntriesToPending(TimeSpan staleTimeout, CancellationToken cancellationToken);

    Task<List<RagOutboxEntry>> ListAsync(RagSearchCriteria criteria, CancellationToken cancellationToken);

    Task ClaimBatchAsync(string instanceId, int batchSize, CancellationToken cancellationToken);

    Task UpsertBatchAsync(IReadOnlyList<RagOutboxEntry> entries, string instanceId,
        CancellationToken cancellationToken);

    Task DeleteBatchAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    Task EnqueueReindexAsync(
        string chunkName,
        string entityType,
        Type entityClrType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of outbox entries in the given status.
    /// Used by health checks to surface permanently-failed entries.
    /// </summary>
    Task<int> CountByStatusAsync(RagOutboxStatus status, CancellationToken cancellationToken = default);
}
