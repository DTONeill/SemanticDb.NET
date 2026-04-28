using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;

namespace SemanticDb.EF.Interceptors;

/// <summary>
/// An EF Core interceptor that writes outbox entries for entities
/// that have a matching chunk definition, within the same transaction.
/// </summary>
internal sealed class RagInterceptor : SaveChangesInterceptor
{
    private readonly SearchableEntityRegistry _registry;

    public RagInterceptor(SearchableEntityRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return result;

        var entries = eventData.Context.ChangeTracker
            .Entries()
            .Where(e =>
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                _registry.IsRegistered(e.Entity.GetType()))
            .ToList();

        if (entries.Count == 0)
            return result;

        var newEntries = entries
            .SelectMany(entry =>
            {
                var entityType = entry.Entity.GetType();
                var entityId = GetEntityId(entry);
                var status = entry.State == EntityState.Deleted
                    ? RagOutboxStatus.PendingDelete
                    : RagOutboxStatus.Pending;

                return _registry
                    .GetRegistrations(entityType)
                    .Select(definition => new RagOutboxEntry
                    {
                        EntityType = entityType.FullName!,
                        EntityId = entityId,
                        ChunkName = definition.ChunkName,
                        Status = status
                    });
            })
            .ToList();

        var chunkNames = newEntries.Select(e => e.ChunkName).Distinct().ToList();
        var entityIds = newEntries.Select(e => e.EntityId).Distinct().ToList();

        // Single batch query — ClaimedBy IS NULL means no worker currently owns the entry,
        // which covers Pending, PendingDelete, and Failed states.
        var existing = await eventData.Context
            .Set<RagOutboxEntry>()
            .Where(o => o.ClaimedBy == null &&
                        chunkNames.Contains(o.ChunkName) &&
                        entityIds.Contains(o.EntityId))
            .ToListAsync(cancellationToken);

        foreach (var newEntry in newEntries)
        {
            var match = existing.FirstOrDefault(e =>
                e.ChunkName == newEntry.ChunkName && e.EntityId == newEntry.EntityId);

            if (match is not null)
            {
                match.Status = newEntry.Status;
                match.Error = null;
                match.RetryCount = 0;
                match.NextRetryAt = null;
                match.ProcessedAt = null;
                match.ClaimedBy = null;
                match.ClaimedAt = null;
            }
            else
            {
                eventData.Context.Set<RagOutboxEntry>().Add(newEntry);
            }
        }

        return result;
    }

    private static string GetEntityId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        string[] keyValues = entry.Metadata.FindPrimaryKey()!
            .Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty)
            .ToArray();

        return string.Join("|", keyValues);
    }
}
