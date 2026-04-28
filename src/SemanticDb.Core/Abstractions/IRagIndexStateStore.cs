using SemanticDb.Core.Models;

namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Manages the persisted version state per chunk definition.
/// </summary>
public interface IRagIndexStateStore
{
    /// <summary>
    /// Returns the index state for the given chunk, or <see langword="null"/> if not yet indexed.
    /// </summary>
    Task<RagIndexState?> FindAsync(string chunkName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically updates the version for the given chunk using optimistic concurrency.
    /// Returns <see langword="true"/> if the update succeeded, or <see langword="false"/> if another
    /// instance concurrently updated the same row.
    /// </summary>
    Task<bool> TryUpdateVersionAsync(
        string chunkName,
        string? expectedCompositeVersion,
        string newCompositeVersion,
        CancellationToken cancellationToken = default);
}
