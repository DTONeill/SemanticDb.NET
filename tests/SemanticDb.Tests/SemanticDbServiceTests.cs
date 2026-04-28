using Microsoft.Extensions.AI;
using Moq;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Services;

namespace SemanticDb.Tests;

public class SemanticDbServiceTests
{
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _embeddingGenerator = new();
    private readonly Mock<IVectorSearch> _vectorSearch = new();
    private readonly SemanticDbOptions _options = new() { DefaultSearchLimit = 25 };
    private readonly SearchableEntityRegistry _registry = new();

    public SemanticDbServiceTests()
    {
        RegisterChunk<FakeSearchableEntity>("FakeChunk");
    }

    private ISemanticDbService CreateService() =>
        new SemanticDbService(_embeddingGenerator.Object, _vectorSearch.Object, _options, _registry);

    private void RegisterChunk<TImpl>(string chunkName) =>
        _registry.Register(new SearchableEntityRegistration
        {
            ChunkName = chunkName,
            ImplementationType = typeof(TImpl),
            EntityType = typeof(object),
            Version = 1,
            ToSearchContent = _ => "",
            ToPromptContext = _ => "",
            GetScopeKey = _ => null,
        });

    private void SetupEmbedding(float[] floats)
    {
        var embeddings = new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(floats)]);
        _embeddingGenerator
            .Setup(e => e.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);
    }

    [Fact]
    public async Task SearchAsync_UsesDefaultLimit_WhenLimitNotProvided()
    {
        SetupEmbedding([1f, 0f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), null, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateService().SearchAsync<FakeSearchableEntity>("query");

        _vectorSearch.Verify(v => v.SearchAsync("FakeChunk", It.IsAny<float[]>(), null, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_UsesProvidedLimit()
    {
        SetupEmbedding([1f, 0f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateService().SearchAsync<FakeSearchableEntity>("query", limit: 10);

        _vectorSearch.Verify(v => v.SearchAsync("FakeChunk", It.IsAny<float[]>(), null, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_PassesScopeKeyToVectorSearch()
    {
        SetupEmbedding([1f, 0f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), "tenant-1", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateService().SearchAsync<FakeSearchableEntity>("query", scopeKey: "tenant-1");

        _vectorSearch.Verify(v => v.SearchAsync("FakeChunk", It.IsAny<float[]>(), "tenant-1", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_PassesQueryEmbeddingToVectorSearch()
    {
        var floats = new float[] { 0.5f, 0.3f, 0.9f };
        SetupEmbedding(floats);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateService().SearchAsync<FakeSearchableEntity>("my query");

        _vectorSearch.Verify(v => v.SearchAsync(
            It.IsAny<string>(),
            It.Is<float[]>(f => f.SequenceEqual(floats)),
            null, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ReturnsResultsFromVectorSearch()
    {
        SetupEmbedding([1f]);
        var expected = new List<SemanticDbResult>
        {
            new() { EntityId = "1", Score = 0.95f, PromptContext = "ctx1" },
            new() { EntityId = "2", Score = 0.80f, PromptContext = "ctx2" },
        };
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var results = await CreateService().SearchAsync<FakeSearchableEntity>("query");

        Assert.Equal(2, results.Count);
        Assert.Equal("1", results[0].EntityId);
        Assert.Equal(0.95f, results[0].Score);
    }

    [Fact]
    public async Task SearchAsync_ResolvesChunkNameFromRegistry()
    {
        SetupEmbedding([1f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateService().SearchAsync<FakeSearchableEntity>("query");

        _vectorSearch.Verify(v => v.SearchAsync("FakeChunk", It.IsAny<float[]>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ThrowsInvalidOperationException_WhenNotRegistered()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().SearchAsync<UnregisteredSearchableEntity>("query"));
    }

    private sealed class FakeSearchableEntity : ISearchableEntity { }
    private sealed class UnregisteredSearchableEntity : ISearchableEntity { }
}
