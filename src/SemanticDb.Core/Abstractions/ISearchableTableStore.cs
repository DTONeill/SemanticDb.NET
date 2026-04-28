namespace SemanticDb.Core.Abstractions;

public interface ISearchableTableStore
{
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
