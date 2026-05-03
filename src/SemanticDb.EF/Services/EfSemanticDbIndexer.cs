using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Configuration;

namespace SemanticDb.EF.Services;

internal sealed class EfSemanticDbIndexer : ISemanticDbIndexer
{
    private readonly DbContext _dbContext;
    private readonly SearchableEntityRegistry _registry;
    private readonly IRagOutboxStore _outboxStore;

    public EfSemanticDbIndexer(DbContext dbContext, SearchableEntityRegistry registry, IRagOutboxStore outboxStore)
    {
        _dbContext = dbContext;
        _registry = registry;
        _outboxStore = outboxStore;
    }

    /// <inheritdoc />
    public Task RequestReindexAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        Type entityType = entity.GetType();
        string entityId = string.Join("|", _dbContext.Model
            .FindEntityType(entityType)!
            .FindPrimaryKey()!.Properties
            .Select(p => p.PropertyInfo!.GetValue(entity)?.ToString() ?? string.Empty));

        return EnqueueEntityAsync(entityType, entityId, cancellationToken);
    }

    /// <inheritdoc />
    public Task RequestReindexAsync<TEntity>(object? keyValue, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        Type entityType = typeof(TEntity);
        var pkProperties = _dbContext.Model.FindEntityType(entityType)!.FindPrimaryKey()!.Properties;

        if (pkProperties.Count != 1)
            throw new InvalidOperationException(
                $"Entity type '{entityType.Name}' has a composite primary key. Use RequestReindexAsync(entity) instead.");

        return EnqueueEntityAsync(entityType, keyValue?.ToString() ?? string.Empty, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RequestReindexAsync<TEntity>(CancellationToken cancellationToken = default) where TEntity : class
    {
        var entityType = typeof(TEntity);

        foreach (var registration in GetRegistrations(entityType))
            await _outboxStore.EnqueueReindexAsync(registration.ChunkName, entityType, cancellationToken);
    }

    private async Task EnqueueEntityAsync(Type entityType, string entityId, CancellationToken cancellationToken)
    {
        foreach (var registration in GetRegistrations(entityType))
            await _outboxStore.EnqueueEntityReindexAsync(
                registration.ChunkName,
                entityType.FullName!,
                entityId,
                cancellationToken);
    }

    private List<SearchableEntityRegistration> GetRegistrations(Type entityType)
    {
        var registrations = _registry.GetRegistrations(entityType).ToList();

        if (registrations.Count == 0)
            throw new InvalidOperationException(
                $"Entity type '{entityType.Name}' has no registered chunk definitions. " +
                $"Ensure it is targeted by an ISearchableEntity implementation and its assembly is scanned by AddSemanticDb.");

        return registrations;
    }
}
