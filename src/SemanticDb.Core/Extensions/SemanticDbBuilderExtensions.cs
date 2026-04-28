using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Health;

namespace SemanticDb.Core.Extensions;

/// <summary>
/// Extension methods for configuring embeddings and health checks on a <see cref="SemanticDbBuilder"/>.
/// </summary>
public static class SemanticDbBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> instance to use for generating embeddings.
    /// </summary>
    public static SemanticDbBuilder UseEmbeddingsProvider(
        this SemanticDbBuilder builder,
        IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        builder.Services.AddKeyedSingleton(SemanticDbBuilder.EmbeddingGeneratorKey, generator);
        return builder;
    }

    /// <summary>
    /// Registers the <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> factory to use for generating embeddings.
    /// </summary>
    public static SemanticDbBuilder UseEmbeddingsProvider(
        this SemanticDbBuilder builder,
        Func<IServiceProvider, IEmbeddingGenerator<string, Embedding<float>>> factory)
    {
        builder.Services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
            SemanticDbBuilder.EmbeddingGeneratorKey, (sp, _) => factory(sp));
        return builder;
    }

    /// <summary>
    /// Adds a <see cref="SemanticDbHealthCheck"/> that reports <see cref="HealthStatus.Degraded"/>
    /// when permanently-failed outbox entries exist.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddHealthChecks().AddSemanticDb();
    /// </code>
    /// </example>
    public static IHealthChecksBuilder AddSemanticDb(
        this IHealthChecksBuilder healthChecksBuilder,
        string name = "semantic_search")
    {
        healthChecksBuilder.Services.AddSingleton<SemanticDbHealthCheck>();
        return healthChecksBuilder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<SemanticDbHealthCheck>(),
            failureStatus: HealthStatus.Degraded,
            tags: ["semantic_search", "outbox"]));
    }
}
