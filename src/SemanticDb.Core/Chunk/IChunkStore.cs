using SemanticDb.Core.Outbox;

namespace SemanticDb.Core.Chunk;

/// <summary>
/// Defines the contract for storing and retrieving semantic search chunks.
/// </summary>
public interface IChunkStore
{
    /// <summary>
    /// Inserts or updates the given chunks in the vector store.
    /// </summary>
    Task UpsertBatchAsync(
        IReadOnlyList<ChunkUpsertEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes chunks matching the given criteria from the vector store.
    /// </summary>
    Task DeleteBatchAsync(
        IReadOnlyList<ChunkEntryDeleteCriteria> criteria,
        CancellationToken cancellationToken = default);
}
