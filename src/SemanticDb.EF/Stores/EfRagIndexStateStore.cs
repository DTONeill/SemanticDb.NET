using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Models;

namespace SemanticDb.EF.Stores;

internal sealed class EfRagIndexStateStore : IRagIndexStateStore
{
    private readonly DbContext _dbContext;

    public EfRagIndexStateStore(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RagIndexState?> FindAsync(string chunkName, CancellationToken cancellationToken = default)
    {
        return _dbContext
            .Set<RagIndexState>()
            .FirstOrDefaultAsync(x => x.ChunkName == chunkName, cancellationToken);
    }

    public async Task UpsertAsync(RagIndexState state, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext
            .Set<RagIndexState>()
            .FirstOrDefaultAsync(x => x.ChunkName == state.ChunkName, cancellationToken);

        if (existing is null)
            _dbContext.Set<RagIndexState>().Add(state);
        else
        {
            existing.CompositeVersion = state.CompositeVersion;
            existing.LastIndexedAt = state.LastIndexedAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryUpdateVersionAsync(
        string chunkName,
        string? expectedCompositeVersion,
        string newCompositeVersion,
        CancellationToken cancellationToken = default)
    {
        if (expectedCompositeVersion is null)
        {
            // Première fois : INSERT si la ligne n'existe pas encore
            var entry = new RagIndexState
            {
                ChunkName = chunkName,
                CompositeVersion = newCompositeVersion,
                LastIndexedAt = DateTime.UtcNow
            };

            try
            {
                _dbContext.Set<RagIndexState>().Add(entry);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException)
            {
                // Une autre instance a inséré en même temps
                _dbContext.Entry(entry).State = EntityState.Detached;
                return false;
            }
        }

        var affected = await _dbContext
            .Set<RagIndexState>()
            .Where(x => x.ChunkName == chunkName && x.CompositeVersion == expectedCompositeVersion)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.CompositeVersion, newCompositeVersion)
                    .SetProperty(x => x.LastIndexedAt, DateTime.UtcNow),
                cancellationToken);

        return affected > 0;
    }
}
