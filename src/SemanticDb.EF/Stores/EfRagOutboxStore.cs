using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;

namespace SemanticDb.EF.Stores;

/// <summary>
/// EF Core implementation of <see cref="SemanticDb.Core.Abstractions.IRagOutboxStore"/>.
/// Uses provider-specific locking hints on SQL Server to allow concurrent processor instances to claim disjoint batches.
/// </summary>
public class EfRagOutboxStore : IRagOutboxStore
{
    private readonly DbContext _dbContext;

    public EfRagOutboxStore(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<List<RagOutboxEntry>> ListAsync(RagSearchCriteria criteria, CancellationToken cancellationToken)
    {
        return _dbContext
            .Set<RagOutboxEntry>()
            .Where(e => e.ClaimedBy == criteria.instanceId
                        && (e.Status == RagOutboxStatus.Processing || e.Status == RagOutboxStatus.ProcessingDelete))
            .Take(criteria.Take)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task SetStaleEntriesToPending(TimeSpan staleTimeout, CancellationToken cancellationToken)
    {
        DateTime staleThreshold = DateTime.UtcNow - staleTimeout;
        return _dbContext
            .Set<RagOutboxEntry>()
            .Where(e => (e.Status == RagOutboxStatus.Processing || e.Status == RagOutboxStatus.ProcessingDelete)
                        && e.ClaimedAt < staleThreshold)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, x =>
                        x.Status == RagOutboxStatus.ProcessingDelete
                            ? RagOutboxStatus.PendingDelete
                            : RagOutboxStatus.Pending)
                    .SetProperty(x => x.ClaimedBy, (string?)null)
                    .SetProperty(x => x.ClaimedAt, (DateTime?)null)
                    .SetProperty(x => x.NextRetryAt, (DateTime?)null),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task ClaimBatchAsync(string instanceId, int batchSize, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // SQL Server: UPDLOCK prevents other readers from taking shared locks on the same rows;
        // READPAST skips rows already locked, so concurrent instances claim disjoint sets.
        if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            return _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE TOP ({batchSize}) RagOutbox WITH (UPDLOCK, READPAST)
                 SET Status      = CASE Status
                                     WHEN {(int)RagOutboxStatus.PendingDelete} THEN {(int)RagOutboxStatus.ProcessingDelete}
                                     ELSE {(int)RagOutboxStatus.Processing}
                                   END,
                     ClaimedBy   = {instanceId},
                     ClaimedAt   = {now}
                 WHERE Status IN ({(int)RagOutboxStatus.Pending}, {(int)RagOutboxStatus.PendingDelete})
                   AND (NextRetryAt IS NULL OR NextRetryAt <= {now})
                 """,
                cancellationToken);
        }

        // Other providers: best-effort (no skip-locked equivalent in EF Core bulk updates)
        return _dbContext
            .Set<RagOutboxEntry>()
            .Where(e => (e.Status == RagOutboxStatus.Pending || e.Status == RagOutboxStatus.PendingDelete)
                        && (e.NextRetryAt == null || e.NextRetryAt <= now))
            .Take(batchSize)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, x =>
                        x.Status == RagOutboxStatus.PendingDelete
                            ? RagOutboxStatus.ProcessingDelete
                            : RagOutboxStatus.Processing)
                    .SetProperty(x => x.ClaimedBy, instanceId)
                    .SetProperty(x => x.ClaimedAt, now),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task UpsertBatchAsync(
        IReadOnlyList<RagOutboxEntry> entries,
        string instanceId,
        CancellationToken cancellationToken)
    {
        // Entries were fetched in this scope so they are already tracked;
        // SaveChangesAsync persists the mutations applied by the caller (e.g. MarkFailed).
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteBatchAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        return _dbContext
            .Set<RagOutboxEntry>()
            .Where(x => ids.Contains(x.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountByStatusAsync(RagOutboxStatus status, CancellationToken cancellationToken = default)
    {
        return _dbContext
            .Set<RagOutboxEntry>()
            .CountAsync(e => e.Status == status, cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnqueueReindexAsync(
        string chunkName,
        Type entityClrType,
        CancellationToken cancellationToken = default)
    {
        var entityMetadata = _dbContext.Model.FindEntityType(entityClrType)!;
        var pkProperties = entityMetadata.FindPrimaryKey()!.Properties;

        const int batchSize = 500;
        var skip = 0;

        var setMethod = typeof(DbContext)
            .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(entityClrType);

        var queryable = (IQueryable<object>)setMethod.Invoke(_dbContext, null)!;

        while (true)
        {
            var batch = await queryable
                .Skip(skip)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            foreach (var entity in batch)
            {
                var entityId = string.Join("|", pkProperties
                    .Select(p => p.PropertyInfo!.GetValue(entity)?.ToString() ?? string.Empty));

                await EnqueueEntityReindexAsync(chunkName, entityClrType.FullName!, entityId, cancellationToken);
            }

            _dbContext.ChangeTracker.Clear();

            if (batch.Count < batchSize)
                break;

            skip += batchSize;
        }
    }

    /// <inheritdoc />
    public async Task EnqueueEntityReindexAsync(
        string chunkName,
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext
            .Set<RagOutboxEntry>()
            .FirstOrDefaultAsync(
                o => o.ClaimedBy == null && o.ChunkName == chunkName && o.EntityId == entityId,
                cancellationToken);

        if (existing is not null)
        {
            existing.Status = RagOutboxStatus.Pending;
            existing.Error = null;
            existing.RetryCount = 0;
            existing.NextRetryAt = null;
            existing.ProcessedAt = null;
            existing.ClaimedBy = null;
            existing.ClaimedAt = null;
        }
        else
        {
            _dbContext.Set<RagOutboxEntry>().Add(new RagOutboxEntry
            {
                EntityType = entityType,
                EntityId = entityId,
                ChunkName = chunkName,
                Status = RagOutboxStatus.Pending
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
