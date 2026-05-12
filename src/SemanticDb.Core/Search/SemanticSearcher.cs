using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;

namespace SemanticDb.Core.Search;

/// <summary>
/// Default implementation of <see cref="ISemanticSearcher{TSearchableEntity}"/>.
/// Resolves the entity's chunk registration at construction time and dispatches
/// execution to the <see cref="ISearchStrategy"/> selected by the query.
/// </summary>
internal sealed class SemanticSearcher<TSearchableEntity> : ISemanticSearcher<TSearchableEntity>
    where TSearchableEntity : ISearchableEntity
{
    private readonly SearchStrategyRegistry _strategyRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemanticDbOptions _options;
    private readonly SearchableEntityRegistration _registration;

    public SemanticSearcher(
        SearchStrategyRegistry strategyRegistry,
        IServiceProvider serviceProvider,
        SemanticDbOptions options,
        SearchableEntityRegistry registry)
    {
        _strategyRegistry = strategyRegistry;
        _serviceProvider = serviceProvider;
        _options = options;

        if (!registry.TryGetByImplementationType(typeof(TSearchableEntity), out var registration))
            throw new InvalidOperationException(
                $"'{typeof(TSearchableEntity).Name}' is not registered as a searchable entity. " +
                $"Ensure it implements ISearchableEntity<T, TScopeKey> and its assembly is passed to AddSemanticDb().");

        _registration = registration!;
    }

    /// <inheritdoc />
    public SemanticSearchQuery<TSearchableEntity> Query(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Query text cannot be null or whitespace.", nameof(text));

        return new SemanticSearchQuery<TSearchableEntity>(text, ExecuteAsync);
    }

    private async Task<IReadOnlyList<SemanticDbResult>> ExecuteAsync(
        SemanticSearchQuery<TSearchableEntity> query,
        CancellationToken cancellationToken)
    {
        Type implType = _strategyRegistry.Resolve(query.StrategyType);
        ISearchStrategy strategy = (ISearchStrategy)_serviceProvider.GetRequiredService(implType);

        return await strategy.ExecuteAsync(new SearchExecutionContext
        {
            QueryText = query.QueryText,
            ChunkName = _registration.ChunkName,
            ScopeKey = query.ScopeKey,
            TopK = query.TopK ?? _options.DefaultSearchLimit
        }, cancellationToken);
    }
}
