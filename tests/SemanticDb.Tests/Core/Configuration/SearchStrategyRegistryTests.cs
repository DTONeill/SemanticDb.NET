using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Search;

namespace SemanticDb.Tests.Core.Configuration;

public class SearchStrategyRegistryTests
{
    [Fact]
    public void Resolve_ReturnsRegisteredImplType()
    {
        var registry = new SearchStrategyRegistry();
        registry.Register(typeof(IConceptA), typeof(ImplA));

        var result = registry.Resolve(typeof(IConceptA));

        Assert.Equal(typeof(ImplA), result);
    }

    [Fact]
    public void Resolve_Throws_WhenConceptNotRegistered()
    {
        var registry = new SearchStrategyRegistry();

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Resolve(typeof(IConceptA)));
        Assert.Contains("IConceptA", ex.Message);
    }

    [Fact]
    public void Register_Overwrites_WhenSameConceptRegisteredTwice()
    {
        var registry = new SearchStrategyRegistry();
        registry.Register(typeof(IConceptA), typeof(ImplA));
        registry.Register(typeof(IConceptA), typeof(ImplB));

        Assert.Equal(typeof(ImplB), registry.Resolve(typeof(IConceptA)));
    }

    [Fact]
    public void RegisteredConcepts_ReturnsAllRegisteredKeys()
    {
        var registry = new SearchStrategyRegistry();
        registry.Register(typeof(IConceptA), typeof(ImplA));
        registry.Register(typeof(IConceptB), typeof(ImplB));

        var concepts = registry.RegisteredConcepts.ToList();

        Assert.Contains(typeof(IConceptA), concepts);
        Assert.Contains(typeof(IConceptB), concepts);
        Assert.Equal(2, concepts.Count);
    }

    [Fact]
    public void RegisteredConcepts_IsEmpty_WhenNothingRegistered()
    {
        var registry = new SearchStrategyRegistry();
        Assert.Empty(registry.RegisteredConcepts);
    }

    private interface IConceptA { }
    private interface IConceptB { }
    private sealed class ImplA : ISearchStrategy
    {
        public Task<IReadOnlyList<SemanticDbResult>> ExecuteAsync(SearchExecutionContext context, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SemanticDbResult>>([]);
    }
    private sealed class ImplB : ISearchStrategy
    {
        public Task<IReadOnlyList<SemanticDbResult>> ExecuteAsync(SearchExecutionContext context, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SemanticDbResult>>([]);
    }
}
