using SemanticDb.Core.Outbox;

namespace SemanticDb.Core.Chunk;

/// <summary>
/// Defines the contract for storing and retrieving semantic search chunks.
/// </summary>
public interface IChunkStore
{
    Task UpsertBatchAsync(
        IReadOnlyList<ChunkUpsertEntry> entries,
        CancellationToken cancellationToken = default);

    Task DeleteBatchAsync(
        IReadOnlyList<ChunkEntryDeleteCriteria> criteria,
        CancellationToken cancellationToken = default);
}
