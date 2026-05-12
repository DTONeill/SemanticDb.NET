using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Search;
using SemanticDb.EF.Search;

namespace SemanticDb.Tests.Core.Search;

public class SemanticSearcherTests
{
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _embeddingGenerator = new();
    private readonly Mock<IVectorSearch> _vectorSearch = new();
    private readonly SemanticDbOptions _options = new() { DefaultSearchLimit = 25 };
    private readonly SearchableEntityRegistry _registry = new();

    public SemanticSearcherTests()
    {
        _registry.Register(new SearchableEntityRegistration
        {
            ChunkName = "FakeChunk",
            ImplementationType = typeof(FakeEntity),
            EntityType = typeof(object),
            Version = 1,
            ToSearchContent = _ => "",
            ToPromptContext = _ => "",
            GetScopeKey = _ => null,
            IsDeleted = _ => false,
        });
    }

    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
            SemanticDbBuilder.EmbeddingGeneratorKey, _embeddingGenerator.Object);
        services.AddScoped<IVectorSearch>(_ => _vectorSearch.Object);
        services.AddScoped<InMemoryVectorSearchStrategy>();
        return services.BuildServiceProvider();
    }

    private ISemanticSearcher<FakeEntity> CreateSearcher()
    {
        var strategyRegistry = new SearchStrategyRegistry();
        strategyRegistry.Register(typeof(IInMemoryVectorSearch), typeof(InMemoryVectorSearchStrategy));
        return new SemanticSearcher<FakeEntity>(strategyRegistry, BuildServiceProvider(), _options, _registry);
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
    public async Task Query_UsesDefaultLimit_WhenLimitNotSet()
    {
        SetupEmbedding([1f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), null, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSearcher().Query("fire").ToListAsync();

        _vectorSearch.Verify(
            v => v.SearchAsync("FakeChunk", It.IsAny<float[]>(), null, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_UsesExplicitLimit_WhenSet()
    {
        SetupEmbedding([1f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), null, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSearcher().Query("fire").Limit(5).ToListAsync();

        _vectorSearch.Verify(
            v => v.SearchAsync("FakeChunk", It.IsAny<float[]>(), null, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_PassesScopeKeyToVectorSearch()
    {
        SetupEmbedding([1f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), "tenant-1", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSearcher().Query("fire").WithScope("tenant-1").ToListAsync();

        _vectorSearch.Verify(
            v => v.SearchAsync("FakeChunk", It.IsAny<float[]>(), "tenant-1", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_PassesEmbeddingToVectorSearch()
    {
        float[] floats = [0.1f, 0.9f, 0.5f];
        SetupEmbedding(floats);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSearcher().Query("query").ToListAsync();

        _vectorSearch.Verify(v => v.SearchAsync(
            It.IsAny<string>(),
            It.Is<float[]>(f => f.SequenceEqual(floats)),
            null, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_ReturnsResultsFromVectorSearch()
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

        var results = await CreateSearcher().Query("query").ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal("1", results[0].EntityId);
        Assert.Equal(0.95f, results[0].Score);
    }

    [Fact]
    public async Task Query_WithIntScopeKey_PassesStringRepresentation()
    {
        SetupEmbedding([1f]);
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), "42", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSearcher().Query("query").WithScope(42).ToListAsync();

        _vectorSearch.Verify(
            v => v.SearchAsync("FakeChunk", It.IsAny<float[]>(), "42", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UseInMemorySearch_ExplicitlySet_ProducesSameResultAsDefault()
    {
        SetupEmbedding([1f]);
        var expected = new List<SemanticDbResult> { new() { EntityId = "1", Score = 0.9f, PromptContext = "ctx" } };
        _vectorSearch
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var results = await CreateSearcher().Query("query").UseInMemorySearch().ToListAsync();

        Assert.Single(results);
        Assert.Equal("1", results[0].EntityId);
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenEntityNotRegistered()
    {
        var strategyRegistry = new SearchStrategyRegistry();
        Assert.Throws<InvalidOperationException>(() =>
            new SemanticSearcher<UnregisteredEntity>(strategyRegistry, BuildServiceProvider(), _options, _registry));
    }

    [Fact]
    public async Task Query_ThrowsInvalidOperationException_WhenStrategyNotRegistered()
    {
        SetupEmbedding([1f]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSearcher().Query("fire").WithStrategy(typeof(IUnregisteredStrategy)).ToListAsync());
    }

    [Fact]
    public void Query_ThrowsArgumentException_WhenTextIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => CreateSearcher().Query("  "));
    }

    private interface IUnregisteredStrategy { }
    private sealed class FakeEntity : ISearchableEntity { }
    private sealed class UnregisteredEntity : ISearchableEntity { }
}
