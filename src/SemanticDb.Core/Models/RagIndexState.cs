namespace SemanticDb.Core.Models;

/// <summary>
/// Tracks the current indexed version per chunk definition.
/// Used to detect when re-indexing is required after a template change.
/// </summary>
public sealed class RagIndexState
{
    /// <summary>
    /// The name of the chunk definition.
    /// </summary>
    public required string ChunkName { get; init; }

    /// <summary>
    /// The composite version string: "{Version}:{ProviderKey}" (e.g. "1:SqlServer").
    /// </summary>
    public required string CompositeVersion { get; set; }

    /// <summary>
    /// The UTC timestamp of the last re-indexing.
    /// </summary>
    public DateTime LastIndexedAt { get; set; } = DateTime.UtcNow;
}
