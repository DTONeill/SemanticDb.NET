using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Search;
using SemanticDb.EF.Search;

namespace SemanticDb.Tests;

public class VectorSearchStrategyTests
{
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _embeddingGenerator = new();
    private readonly Mock<IVectorSearch> _vectorSearch = new();

    private InMemoryVectorSearchStrategy CreateStrategy()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
            SemanticDbBuilder.EmbeddingGeneratorKey, _embeddingGenerator.Object);
        services.AddScoped<IVectorSearch>(_ => _vectorSearch.Object);
        services.AddScoped<InMemoryVectorSearchStrategy>();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<InMemoryVectorSearchStrategy>();
    }

    private void SetupEmbedding(float[] floats)
    {
        var embeddings = new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(floats)]);
        _embeddingGenerator
            .Setup(e => e.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesEmbeddingFromQueryText()
    {
        SetupEmbedding([0.1f, 0.2f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ctx = new SearchExecutionContext { QueryText = "my query", ChunkName = "Chunk", TopK = 10 };
        await CreateStrategy().ExecuteAsync(ctx, default);

        _embeddingGenerator.Verify(
            e => e.GenerateAsync(
                It.Is<IEnumerable<string>>(texts => texts.Single() == "my query"),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesEmbeddingToVectorSearch()
    {
        float[] floats = [0.3f, 0.7f, 0.1f];
        SetupEmbedding(floats);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ctx = new SearchExecutionContext { QueryText = "q", ChunkName = "Chunk", TopK = 5 };
        await CreateStrategy().ExecuteAsync(ctx, default);

        _vectorSearch.Verify(
            v => v.SearchAsync(
                It.IsAny<string>(),
                It.Is<float[]>(f => f.SequenceEqual(floats)),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesChunkNameToVectorSearch()
    {
        SetupEmbedding([1f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ctx = new SearchExecutionContext { QueryText = "q", ChunkName = "MyChunk", TopK = 5 };
        await CreateStrategy().ExecuteAsync(ctx, default);

        _vectorSearch.Verify(
            v => v.SearchAsync("MyChunk", It.IsAny<float[]>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesScopeKeyToVectorSearch()
    {
        SetupEmbedding([1f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), "tenant-7", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ctx = new SearchExecutionContext { QueryText = "q", ChunkName = "Chunk", ScopeKey = "tenant-7", TopK = 5 };
        await CreateStrategy().ExecuteAsync(ctx, default);

        _vectorSearch.Verify(
            v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), "tenant-7", It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesTopKToVectorSearch()
    {
        SetupEmbedding([1f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string?>(), 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ctx = new SearchExecutionContext { QueryText = "q", ChunkName = "Chunk", TopK = 42 };
        await CreateStrategy().ExecuteAsync(ctx, default);

        _vectorSearch.Verify(
            v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string?>(), 42, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsResultsFromVectorSearch()
    {
        SetupEmbedding([1f]);
        var expected = new List<SemanticDbResult>
        {
            new() { EntityId = "abc", Score = 0.88f, PromptContext = "ctx" },
        };
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var ctx = new SearchExecutionContext { QueryText = "q", ChunkName = "Chunk", TopK = 5 };
        var results = await CreateStrategy().ExecuteAsync(ctx, default);

        Assert.Single(results);
        Assert.Equal("abc", results[0].EntityId);
        Assert.Equal(0.88f, results[0].Score);
    }
}
