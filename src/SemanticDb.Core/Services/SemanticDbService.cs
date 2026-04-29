using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;

namespace SemanticDb.Core.Services;

/// <summary>
/// Orchestrates semantic search by generating embeddings and delegating vector search
/// to the configured <see cref="IVectorSearch"/> provider.
/// </summary>
internal sealed class SemanticDbService : ISemanticDbService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorSearch _vectorSearch;
    private readonly SemanticDbOptions _options;
    private readonly SearchableEntityRegistry _registry;

    public SemanticDbService(
        [FromKeyedServices(SemanticDbBuilder.EmbeddingGeneratorKey)]
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorSearch vectorSearch,
        SemanticDbOptions options,
        SearchableEntityRegistry registry)
    {
        _embeddingGenerator = embeddingGenerator;
        _vectorSearch = vectorSearch;
        _options = options;
        _registry = registry;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SemanticDbResult>> SearchAsync<TSearchableEntity>(
        string query,
        int? limit = null,
        CancellationToken cancellationToken = default)
        where TSearchableEntity : ISearchableEntity
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));

        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");

        if (!_registry.TryGetByImplementationType(typeof(TSearchableEntity), out var registration))
            throw new InvalidOperationException(
                $"'{typeof(TSearchableEntity).Name}' is not registered as a searchable entity.");

        return SearchAsync(registration!.ChunkName, query, scopeKey: null, limit, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SemanticDbResult>> SearchAsync<TSearchableEntity, TScopeKey>(
        string query,
        TScopeKey scopeKey,
        int? limit = null,
        CancellationToken cancellationToken = default)
        where TSearchableEntity : ISearchableEntity<TScopeKey>
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));

        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");

        if (!_registry.TryGetByImplementationType(typeof(TSearchableEntity), out var registration))
            throw new InvalidOperationException(
                $"'{typeof(TSearchableEntity).Name}' is not registered as a searchable entity.");

        return SearchAsync(registration!.ChunkName, query, scopeKey?.ToString(), limit, cancellationToken);
    }

    private async Task<IReadOnlyList<SemanticDbResult>> SearchAsync(
        string chunkName,
        string query,
        string? scopeKey = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _embeddingGenerator.GenerateVectorAsync(query, cancellationToken: cancellationToken);

        return await _vectorSearch.SearchAsync(
            chunkName,
            embedding.ToArray(),
            scopeKey,
            limit ?? _options.DefaultSearchLimit,
            cancellationToken);
    }
}
