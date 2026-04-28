namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Loads entity data from the underlying store for chunk generation during indexing.
/// </summary>
public interface ISearchableTableStore
{
    /// <summary>
    /// Loads a batch of entities by their serialized primary keys. Returns a dictionary keyed by entity ID.
    /// Missing entities are represented as <see langword="null"/> values.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, object?>> LoadEntitiesBatchAsync(
        Type entityType,
        IReadOnlyList<string> entityIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns all entity IDs of the given type for re-indexing purposes.
    /// </summary>
    ValueTask<IReadOnlyList<string>> LoadAllEntityIdsAsync(
        Type entityType,
        CancellationToken cancellationToken = default);
}
