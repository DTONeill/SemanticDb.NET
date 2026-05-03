namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Provides a way to explicitly enqueue an entity for reindexing, regardless of how it was modified.
/// Use this when an entity is updated outside of EF Core (e.g. raw SQL, bulk operations, external systems)
/// so the change-tracking interceptor would not have fired.
/// </summary>
public interface ISemanticDbIndexer
{
    /// <summary>
    /// Enqueues the single entity identified by <paramref name="entity"/>'s primary key for reindexing.
    /// One outbox entry is created per chunk definition registered for <typeparamref name="TEntity"/>.
    /// An existing unclaimed entry for the same key is reset rather than duplicated.
    /// </summary>
    /// <typeparam name="TEntity">The entity type. Must be registered via <c>AddSemanticDb</c>.</typeparam>
    /// <param name="entity">The entity instance whose primary key identifies the outbox entry to enqueue.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task RequestReindexAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Enqueues the entity identified by <paramref name="keyValue"/> for reindexing.
    /// Use this when you already know the primary key and do not have an entity instance available.
    /// One outbox entry is created per chunk definition registered for <typeparamref name="TEntity"/>.
    /// An existing unclaimed entry for the same key is reset rather than duplicated.
    /// </summary>
    /// <typeparam name="TEntity">The entity type. Must be registered via <c>AddSemanticDb</c>.</typeparam>
    /// <param name="keyValue">The primary key value of the entity to enqueue.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <typeparamref name="TEntity"/> has a composite primary key. Use the entity overload instead.
    /// </exception>
    Task RequestReindexAsync<TEntity>(object? keyValue, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Enqueues all entities of type <typeparamref name="TEntity"/> for reindexing.
    /// One outbox entry is created per chunk definition registered for <typeparamref name="TEntity"/>.
    /// An existing unclaimed entry for the same key is reset rather than duplicated.
    /// </summary>
    /// <typeparam name="TEntity">The entity type. Must be registered via <c>AddSemanticDb</c>.</typeparam>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task RequestReindexAsync<TEntity>(CancellationToken cancellationToken = default)
        where TEntity : class;
}
