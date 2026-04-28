using SemanticDb.Core.Configuration;

namespace SemanticDb.Tests;

public class SearchableEntityRegistryTests
{
    private static SearchableEntityRegistration MakeReg(string chunkName = "TestChunk", Type? type = null, Type? implType = null) =>
        new()
        {
            ChunkName = chunkName,
            ImplementationType = implType ?? typeof(FakeSearchableEntity),
            EntityType = type ?? typeof(FakeEntity),
            Version = 1,
            ToSearchContent = _ => "content",
            ToPromptContext = _ => "context",
            GetScopeKey = _ => null,
        };

    [Fact]
    public void Register_CanLookupByChunkName()
    {
        var registry = new SearchableEntityRegistry();
        var reg = MakeReg();

        registry.Register(reg);

        Assert.True(registry.TryGetByChunkName("TestChunk", out var found));
        Assert.Same(reg, found);
    }

    [Fact]
    public void Register_ThrowsOnDuplicateChunkName()
    {
        var registry = new SearchableEntityRegistry();
        registry.Register(MakeReg());

        Assert.Throws<InvalidOperationException>(() => registry.Register(MakeReg()));
    }

    [Fact]
    public void IsRegistered_ReturnsTrue_ForRegisteredType()
    {
        var registry = new SearchableEntityRegistry();
        registry.Register(MakeReg(type: typeof(FakeEntity)));

        Assert.True(registry.IsRegistered(typeof(FakeEntity)));
    }

    [Fact]
    public void IsRegistered_ReturnsFalse_ForUnknownType()
    {
        var registry = new SearchableEntityRegistry();

        Assert.False(registry.IsRegistered(typeof(FakeEntity)));
    }

    [Fact]
    public void TryGetByChunkName_ReturnsFalse_ForUnknownChunk()
    {
        var registry = new SearchableEntityRegistry();

        Assert.False(registry.TryGetByChunkName("unknown", out _));
    }

    [Fact]
    public void GetRegistrations_ByType_ReturnsMultiple_WhenSameTypeRegisteredTwice()
    {
        var registry = new SearchableEntityRegistry();
        var reg1 = MakeReg("Chunk1", typeof(FakeEntity), typeof(FakeSearchableEntity));
        var reg2 = MakeReg("Chunk2", typeof(FakeEntity), typeof(AnotherSearchableEntity));
        registry.Register(reg1);
        registry.Register(reg2);

        var results = registry.GetRegistrations(typeof(FakeEntity)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(reg1, results);
        Assert.Contains(reg2, results);
    }

    [Fact]
    public void GetRegistrations_ByType_ReturnsEmpty_WhenNotRegistered()
    {
        var registry = new SearchableEntityRegistry();

        Assert.Empty(registry.GetRegistrations(typeof(FakeEntity)));
    }

    [Fact]
    public void GetRegistrations_All_ReturnsAll()
    {
        var registry = new SearchableEntityRegistry();
        registry.Register(MakeReg("Chunk1", typeof(FakeEntity)));
        registry.Register(MakeReg("Chunk2", typeof(AnotherEntity)));

        Assert.Equal(2, registry.GetRegistrations().Count());
    }

    [Fact]
    public void TryGetByImplementationType_ReturnsRegistration_WhenFound()
    {
        var registry = new SearchableEntityRegistry();
        var reg = MakeReg(implType: typeof(FakeSearchableEntity));
        registry.Register(reg);

        Assert.True(registry.TryGetByImplementationType(typeof(FakeSearchableEntity), out var found));
        Assert.Same(reg, found);
    }

    [Fact]
    public void TryGetByImplementationType_ReturnsFalse_WhenNotFound()
    {
        var registry = new SearchableEntityRegistry();

        Assert.False(registry.TryGetByImplementationType(typeof(FakeSearchableEntity), out _));
    }

    private sealed class FakeEntity { }
    private sealed class AnotherEntity { }
    private sealed class FakeSearchableEntity { }
    private sealed class AnotherSearchableEntity { }
}
