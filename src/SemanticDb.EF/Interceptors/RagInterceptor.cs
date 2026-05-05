using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;

namespace SemanticDb.EF.Interceptors;

/// <summary>
/// An EF Core interceptor that writes outbox entries for entities
/// that have a matching chunk definition, within the same transaction.
/// </summary>
/// <remarks>
/// Entities whose primary key is database-generated (e.g. auto-increment int) have a
/// temporary negative ID until the INSERT completes. Those entries are deferred to
/// <see cref="SavedChangesAsync"/> so that the outbox receives the real key value.
/// </remarks>
internal sealed class RagInterceptor : SaveChangesInterceptor
{
    private readonly SearchableEntityRegistry _registry;

    // Keyed by DbContext instance; weak references so GC can collect completed contexts.
    // Value: entities whose outbox entries must be written after the INSERT assigns real IDs.
    private readonly ConditionalWeakTable<DbContext, List<(Type EntityType, object Entity)>> _pendingAdded = new();

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

        List<EntityEntry> readyEntries = [];
        List<(Type EntityType, object Entity)> deferred = [];

        foreach (var entry in entries)
        {
            // Added entities with a database-generated key don't have their real ID yet.
            // Defer them to SavedChangesAsync where the ID is available.
            if (entry.State == EntityState.Added && HasTemporaryKey(entry))
            {
                deferred.Add((entry.Entity.GetType(), entry.Entity));
            }
            else
            {
                readyEntries.Add(entry);
            }
        }

        if (deferred.Count > 0)
        {
            _pendingAdded.AddOrUpdate(eventData.Context, deferred);
        }

        if (readyEntries.Count == 0)
            return result;

        await WriteOutboxEntriesAsync(eventData.Context, readyEntries, cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return result;

        if (!_pendingAdded.TryGetValue(eventData.Context, out var deferred))
            return result;

        _pendingAdded.Remove(eventData.Context);

        // Entities now have their real database-assigned IDs.
        var newEntries = deferred
            .SelectMany(d =>
            {
                var entry = eventData.Context.Entry(d.Entity);
                var entityId = GetEntityId(entry);

                return _registry
                    .GetRegistrations(d.EntityType)
                    .Select(definition => new RagOutboxEntry
                    {
                        EntityType = d.EntityType.FullName!,
                        EntityId = entityId,
                        ChunkName = definition.ChunkName,
                        Status = definition.IsDeleted(d.Entity)
                            ? RagOutboxStatus.PendingDelete
                            : RagOutboxStatus.Pending
                    });
            })
            .ToList();

        if (newEntries.Count == 0)
            return result;

        // These are brand-new entities so no existing outbox entry can conflict; just add.
        eventData.Context.Set<RagOutboxEntry>().AddRange(newEntries);

        // This second SaveChanges only writes RagOutboxEntry rows. The interceptor re-enters
        // SavingChangesAsync but finds no registered entities to process (RagOutboxEntry is
        // not in the registry), so it exits immediately — no recursion.
        await eventData.Context.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        // Clean up deferred state so a retry on the same context starts fresh.
        if (eventData.Context is not null)
        {
            _pendingAdded.Remove(eventData.Context);
        }

        return Task.CompletedTask;
    }

    private async Task WriteOutboxEntriesAsync(
        DbContext context,
        List<EntityEntry> entries,
        CancellationToken cancellationToken)
    {
        var newEntries = entries
            .SelectMany(entry =>
            {
                var entityType = entry.Entity.GetType();
                var entityId = GetEntityId(entry);

                return _registry
                    .GetRegistrations(entityType)
                    .Select(definition =>
                    {
                        RagOutboxStatus status =
                            entry.State == EntityState.Deleted || definition.IsDeleted(entry.Entity)
                                ? RagOutboxStatus.PendingDelete
                                : RagOutboxStatus.Pending;
                        return new RagOutboxEntry
                        {
                            EntityType = entityType.FullName!,
                            EntityId = entityId,
                            ChunkName = definition.ChunkName,
                            Status = status
                        };
                    });
            })
            .ToList();

        var chunkNames = newEntries.Select(e => e.ChunkName).Distinct().ToList();
        var entityIds = newEntries.Select(e => e.EntityId).Distinct().ToList();

        // Single batch query — ClaimedBy IS NULL means no worker currently owns the entry,
        // which covers Pending, PendingDelete, and Failed states.
        var existingLookup = await context
            .Set<RagOutboxEntry>()
            .Where(o => o.ClaimedBy == null &&
                        chunkNames.Contains(o.ChunkName) &&
                        entityIds.Contains(o.EntityId))
            .ToDictionaryAsync(e => (e.ChunkName, e.EntityId), cancellationToken);

        foreach (var newEntry in newEntries)
        {
            if (existingLookup.TryGetValue((newEntry.ChunkName, newEntry.EntityId), out var match))
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
                context.Set<RagOutboxEntry>().Add(newEntry);
            }
        }
    }

    private static bool HasTemporaryKey(EntityEntry entry) =>
        entry.Metadata.FindPrimaryKey()!.Properties
            .Any(p => entry.Property(p.Name).IsTemporary);

    private static string GetEntityId(EntityEntry entry)
    {
        string[] keyValues = entry.Metadata.FindPrimaryKey()!
            .Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty)
            .ToArray();

        return string.Join("|", keyValues);
    }
}
