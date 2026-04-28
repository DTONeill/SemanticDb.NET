using Microsoft.Extensions.AI;

namespace MtgWebApiSample.Tests.Infrastructure;

/// <summary>
/// Returns deterministic 4-dimensional unit vectors based on a keyword in the input text.
/// Texts that share the same keyword yield cosine similarity = 1, enabling predictable test assertions.
/// </summary>
internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private static readonly (string Keyword, int Dim)[] Mappings =
    [
        ("fire",   0),
        ("water",  1),
        ("nature", 2),
    ];

    public EmbeddingGeneratorMetadata Metadata { get; } =
        new("fake", null, "fake-model", 4);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values
            .Select(v => new Embedding<float>(VectorFor(v)))
            .ToList();

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    public object? GetService(Type serviceType, object? serviceKey) => null;

    public void Dispose() { }

    internal static float[] VectorFor(string text)
    {
        var lower = text.ToLowerInvariant();
        foreach (var (keyword, dim) in Mappings)
        {
            if (lower.Contains(keyword))
            {
                var v = new float[4];
                v[dim] = 1f;
                return v;
            }
        }

        return [0f, 0f, 0f, 1f];
    }
}
