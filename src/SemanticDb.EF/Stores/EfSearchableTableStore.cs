using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SemanticDb.Core.Abstractions;

namespace SemanticDb.EF.Stores;

/// <summary>
/// EF Core implementation of <see cref="SemanticDb.Core.Abstractions.ISearchableTableStore"/>.
/// Uses compiled expression delegates cached per entity type for efficient batch loading.
/// </summary>
public class EfSearchableTableStore : ISearchableTableStore
{
    private readonly DbContext _dbContext;

    // Compiled delegates per entity type — built once, reused every batch cycle.
    private static readonly ConcurrentDictionary<Type, Func<EfSearchableTableStore, IReadOnlyList<string>,
            CancellationToken, Task<IReadOnlyDictionary<string, object?>>>>
        LoaderCache = new();

    public EfSearchableTableStore(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Loads a single entity by its serialized primary key.
    /// </summary>
    public ValueTask<object?> LoadEntityAsync(Type entityType, string entityId, CancellationToken cancellationToken)
    {
        IEntityType entityMetadata = _dbContext.Model.FindEntityType(entityType)!;
        IKey primaryKey = entityMetadata.FindPrimaryKey()!;

        object[] keyValues = primaryKey.Properties
            .Zip(entityId.Split('|'), (prop, raw) => ParseKey(raw, prop.ClrType))
            .ToArray();

        return _dbContext.FindAsync(entityType, keyValues, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, object?>> LoadEntitiesBatchAsync(
        Type entityType,
        IReadOnlyList<string> entityIds,
        CancellationToken cancellationToken)
    {
        if (entityIds.Count == 0)
            return new Dictionary<string, object?>();

        IEntityType entityMetadata = _dbContext.Model.FindEntityType(entityType)!;
        IKey primaryKey = entityMetadata.FindPrimaryKey()!;

        if (primaryKey.Properties.Count == 1)
        {
            var pkProp = primaryKey.Properties[0];
            var loader = LoaderCache.GetOrAdd(
                entityType,
                t => CompileLoader(t, pkProp.Name, pkProp.ClrType));
            return await loader(this, entityIds, cancellationToken);
        }

        // Composite PK: sequential fallback
        var result = new Dictionary<string, object?>();
        foreach (var entityId in entityIds)
        {
            var keyValues = primaryKey.Properties
                .Zip(entityId.Split('|'), (prop, raw) => ParseKey(raw, prop.ClrType))
                .ToArray();
            result[entityId] = await _dbContext.FindAsync(entityType, keyValues, cancellationToken);
        }

        return result;
    }

    // Compiles a strongly-typed delegate that calls LoadBySinglePkAsync<TEntity> with
    // pkName and pkType baked in. Runs once per entity type via LoaderCache.GetOrAdd.
    private static Func<EfSearchableTableStore, IReadOnlyList<string>, CancellationToken,
            Task<IReadOnlyDictionary<string, object?>>>
        CompileLoader(Type entityType, string pkName, Type pkType)
    {
        var method = typeof(EfSearchableTableStore)
            .GetMethod(nameof(LoadBySinglePkAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        var storeParam = Expression.Parameter(typeof(EfSearchableTableStore), "store");
        var idsParam = Expression.Parameter(typeof(IReadOnlyList<string>), "ids");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var body = Expression.Call(
            storeParam, method,
            Expression.Constant(pkName),
            Expression.Constant(pkType),
            idsParam,
            ctParam);

        return Expression
            .Lambda<Func<EfSearchableTableStore, IReadOnlyList<string>, CancellationToken,
                Task<IReadOnlyDictionary<string, object?>>>>(
                body, storeParam, idsParam, ctParam)
            .Compile();
    }

    private async Task<IReadOnlyList<string>> LoadAllSinglePkIdsAsync<TEntity, TPk>(
        string pkName, CancellationToken cancellationToken)
        where TEntity : class
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var selector = Expression.Lambda<Func<TEntity, TPk>>(
            Expression.Property(param, pkName), param);

        var values = await _dbContext.Set<TEntity>()
            .AsNoTracking()
            .Select(selector)
            .ToListAsync(cancellationToken);

        return values.Select(v => v?.ToString() ?? string.Empty).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> LoadAllEntityIdsAsync(
        Type entityType,
        CancellationToken cancellationToken = default)
    {
        var entityMetadata = _dbContext.Model.FindEntityType(entityType)!;
        var primaryKey = entityMetadata.FindPrimaryKey()!;

        if (primaryKey.Properties.Count == 1)
        {
            var pkProp = primaryKey.Properties[0];
            return await (Task<IReadOnlyList<string>>)typeof(EfSearchableTableStore)
                .GetMethod(nameof(LoadAllSinglePkIdsAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType, pkProp.ClrType)
                .Invoke(this, [pkProp.Name, cancellationToken])!;
        }

        // Composite PK: project to PK properties only is complex; use AsNoTracking
        // so at least the full entity graph is not retained in the change tracker.
        var setMethod = typeof(DbContext)
            .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(entityType);

        var queryable = ((IQueryable<object>)setMethod.Invoke(_dbContext, null)!)
            .AsNoTracking();

        var entities = await queryable.ToListAsync(cancellationToken);

        return entities
            .Select(entity =>
            {
                var keyValues = primaryKey.Properties
                    .Select(p => entity.GetType().GetProperty(p.Name)!.GetValue(entity)?.ToString() ?? string.Empty)
                    .ToArray();
                return string.Join("|", keyValues);
            })
            .ToList()
            .AsReadOnly();
    }

    private async Task<IReadOnlyDictionary<string, object?>> LoadBySinglePkAsync<TEntity>(
        string pkName, Type pkType, IReadOnlyList<string> entityIds, CancellationToken cancellationToken)
        where TEntity : class
    {
        var typedIds = entityIds.Select(id => ParseKey(id, pkType)).ToList();

        // Build: e => typedIdList.Contains(e.PkName)
        var param = Expression.Parameter(typeof(TEntity), "e");
        var pkAccess = Expression.Property(param, pkName);

        var listType = typeof(List<>).MakeGenericType(pkType);
        var typedList = Activator.CreateInstance(listType)!;
        var addMethod = listType.GetMethod(nameof(List<int>.Add))!;
        foreach (object id in typedIds)
            addMethod.Invoke(typedList, [id]);

        var containsMethod = listType.GetMethod(nameof(List<int>.Contains))!;
        var body = Expression.Call(Expression.Constant(typedList), containsMethod, pkAccess);
        var predicate = Expression.Lambda<Func<TEntity, bool>>(body, param);

        var entities = await _dbContext.Set<TEntity>()
            .Where(predicate)
            .ToListAsync(cancellationToken);

        var pkPropInfo = typeof(TEntity).GetProperty(pkName)!;

        // Build a lookup: parsed key value => entity
        var entityMap = entities.ToDictionary(
            entity => pkPropInfo.GetValue(entity)!,
            entity => (object?)entity);

        var dict = new Dictionary<string, object?>();

        foreach (var (originalId, parsedKey) in entityIds.Zip(typedIds))
        {
            entityMap.TryGetValue(parsedKey, out object? entity);
            dict[originalId] = entity;
        }

        return dict;
    }

    private static object ParseKey(string raw, Type targetType)
    {
        if (targetType == typeof(Guid)) return Guid.Parse(raw);
        return Convert.ChangeType(raw, targetType);
    }
}
