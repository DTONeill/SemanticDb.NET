using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Search;

namespace SemanticDb.EF.Search;

/// <summary>
/// <see cref="ISearchStrategy"/> implementation that generates an embedding for the query text
/// and delegates to the registered <see cref="IVectorSearch"/> provider for in-memory cosine similarity.
/// Registered as the default strategy by <c>UseEfCore()</c>.
/// </summary>
internal sealed class InMemoryVectorSearchStrategy : ISearchStrategy
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorSearch _vectorSearch;

    public InMemoryVectorSearchStrategy(
        [FromKeyedServices(SemanticDbBuilder.EmbeddingGeneratorKey)]
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorSearch vectorSearch)
    {
        _embeddingGenerator = embeddingGenerator;
        _vectorSearch = vectorSearch;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemanticDbResult>> ExecuteAsync(SearchExecutionContext ctx, CancellationToken ct)
    {
        var embedding = await _embeddingGenerator.GenerateVectorAsync(ctx.QueryText, cancellationToken: ct);

        return await _vectorSearch.SearchAsync(
            ctx.ChunkName,
            embedding.ToArray(),
            ctx.ScopeKey,
            ctx.TopK,
            ct);
    }
}
