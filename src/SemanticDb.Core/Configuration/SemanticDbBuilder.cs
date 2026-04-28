using Microsoft.Extensions.DependencyInjection;

namespace SemanticDb.Core.Configuration;

/// <summary>
/// Provides a fluent interface to register provider-specific semantic search services.
/// </summary>
public sealed class SemanticDbBuilder
{
    public IServiceCollection Services { get; }
    public SemanticDbOptions Options { get; }
    public SearchableEntityRegistry Registry { get; }

    /// <summary>
    /// A key identifying the active vector provider.
    /// Used to detect provider changes and trigger automatic re-indexing.
    /// </summary>
    public string? ProviderKey { get; set; }

    public const string EmbeddingGeneratorKey = "SemanticDb.EmbeddingGenerator";

    internal SemanticDbBuilder(
        IServiceCollection services,
        SemanticDbOptions options,
        SearchableEntityRegistry registry)
    {
        Services = services;
        Options = options;
        Registry = registry;
    }
}
