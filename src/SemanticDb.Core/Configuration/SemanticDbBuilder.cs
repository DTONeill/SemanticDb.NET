using Microsoft.Extensions.DependencyInjection;

namespace SemanticDb.Core.Configuration;

/// <summary>
/// Provides a fluent interface to register provider-specific semantic search services.
/// </summary>
public sealed class SemanticDbBuilder
{
    /// <summary>The service collection used to register dependencies.</summary>
    public IServiceCollection Services { get; }

    /// <summary>The options configured for this semantic search instance.</summary>
    public SemanticDbOptions Options { get; }

    /// <summary>The registry of all discovered <see cref="SemanticDb.Core.Abstractions.ISearchableEntity{T}"/> implementations.</summary>
    public SearchableEntityRegistry Registry { get; }

    /// <summary>
    /// The registry that maps search concept interfaces to their <see cref="SemanticDb.Core.Search.ISearchStrategy"/>
    /// implementation types. Provider packages call <see cref="SearchStrategyRegistry.Register"/> here
    /// to make their strategies available at query time.
    /// </summary>
    public SearchStrategyRegistry StrategyRegistry { get; }

    /// <summary>
    /// A key identifying the active vector provider.
    /// Used to detect provider changes and trigger automatic re-indexing.
    /// </summary>
    public string? ProviderKey { get; set; }

    /// <summary>Keyed service key used to resolve the <see cref="Microsoft.Extensions.AI.IEmbeddingGenerator{TInput,TEmbedding}"/> registered via <c>UseEmbeddingsProvider</c>.</summary>
    public const string EmbeddingGeneratorKey = "SemanticDb.EmbeddingGenerator";

    internal SemanticDbBuilder(
        IServiceCollection services,
        SemanticDbOptions options,
        SearchableEntityRegistry registry,
        SearchStrategyRegistry strategyRegistry)
    {
        Services = services;
        Options = options;
        Registry = registry;
        StrategyRegistry = strategyRegistry;
    }
}
