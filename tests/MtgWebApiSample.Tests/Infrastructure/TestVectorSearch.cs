using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Models;

namespace MtgWebApiSample.Tests.Infrastructure;

internal sealed class TestVectorSearch : IVectorSearch
{
    private readonly DbContext _db;

    public TestVectorSearch(DbContext db) => _db = db;

    public async Task<IReadOnlyList<SemanticDbResult>> SearchAsync(
        string chunkName,
        float[] queryVector,
        string? scopeKey,
        int limit,
        CancellationToken cancellationToken)
    {
        var chunks = await _db.Set<RagChunk>()
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
        float dot = 0f, magA = 0f, magB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        float denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom == 0f ? 0f : dot / denom;
    }
}
