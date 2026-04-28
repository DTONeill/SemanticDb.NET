using SemanticDb.Core.Models;

namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Manages the persisted version state per chunk definition.
/// </summary>
public interface IRagIndexStateStore
{
    Task<RagIndexState?> FindAsync(string chunkName, CancellationToken cancellationToken = default);

    Task<bool> TryUpdateVersionAsync(
        string chunkName,
        string? expectedCompositeVersion,
        string newCompositeVersion,
        CancellationToken cancellationToken = default);
}
