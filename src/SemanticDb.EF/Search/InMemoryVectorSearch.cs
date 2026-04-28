using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Models;

namespace SemanticDb.EF.Search;

/// <summary>
/// An <see cref="IVectorSearch"/> implementation that loads chunks from the database
/// and computes cosine similarity in memory.
/// Suitable for development and low-volume scenarios.
/// </summary>
internal sealed class InMemoryVectorSearch : IVectorSearch
{
    private readonly DbContext _dbContext;
    private readonly ILogger<InMemoryVectorSearch> _logger;

    public InMemoryVectorSearch(DbContext dbContext, ILogger<InMemoryVectorSearch> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _logger.LogWarning(
            "In-memory vector search is active. This loads all chunks into memory and is not suitable for production. " +
            "Use a provider such as UseSqlServer<TContext>() for production workloads.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemanticDbResult>> SearchAsync(
        string chunkName,
        float[] queryVector,
        string? scopeKey,
        int limit,
        CancellationToken cancellationToken)
    {
        var chunks = await _dbContext.Set<RagChunk>()
            .Where(x => x.ChunkName == chunkName && (scopeKey == null || x.ScopeKey == scopeKey))
            .ToListAsync(cancellationToken);

        return chunks
            .Select(x => new SemanticDbResult
            {
                EntityId = x.EntityId,
                ScopeKey = x.ScopeKey,
                Score = CosineSimilarity(queryVector, x.Embedding),
                PromptContext = x.PromptContext
            })
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .ToList()
            .AsReadOnly();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        var dot = 0f;
        var magA = 0f;
        var magB = 0f;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denominator == 0f ? 0f : dot / denominator;
    }
}
