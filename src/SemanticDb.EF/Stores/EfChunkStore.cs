using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;

namespace SemanticDb.EF.Stores;

internal sealed class EfChunkStore<TContext> : IChunkStore
    where TContext : DbContext
{
    private readonly TContext _db;

    public EfChunkStore(TContext db) => _db = db;

    public Task DeleteAsync(
        ChunkEntryDeleteCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        return _db.Set<RagChunk>()
            .Where(x => x.ChunkName == criteria.ChunkName && x.EntityId == criteria.EntityId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task UpsertBatchAsync(
        IReadOnlyList<ChunkUpsertEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0) return;

        foreach (var group in entries.GroupBy(e => e.ChunkName))
        {
            var entityIds = group.Select(e => e.EntityId).ToList();
            var existing = await _db
                .Set<RagChunk>()
                .Where(c => c.ChunkName == group.Key && entityIds.Contains(c.EntityId))
                .ToListAsync(cancellationToken);

            var lookup = existing.ToDictionary(c => c.EntityId);

            foreach (var entry in group)
            {
                if (lookup.TryGetValue(entry.EntityId, out var chunk))
                {
                    chunk.ScopeKey = entry.ScopeKey;
                    chunk.PromptContext = entry.PromptContext;
                    chunk.Embedding = entry.Embedding.ToArray();
                    chunk.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.Set<RagChunk>().Add(new RagChunk
                    {
                        ChunkName = entry.ChunkName,
                        EntityId = entry.EntityId,
                        ScopeKey = entry.ScopeKey,
                        PromptContext = entry.PromptContext,
                        Embedding = entry.Embedding.ToArray(),
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBatchAsync(
        IReadOnlyList<ChunkEntryDeleteCriteria> criteria,
        CancellationToken cancellationToken = default)
    {
        if (criteria.Count == 0) return;

        foreach (var group in criteria.GroupBy(c => c.ChunkName))
        {
            var entityIds = group.Select(c => c.EntityId).ToList();
            await _db.Set<RagChunk>()
                .Where(c => c.ChunkName == group.Key && entityIds.Contains(c.EntityId))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
