using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Search;

namespace SemanticDb.Tests;

public class SemanticSearchQueryTests
{
    private static SemanticSearchQuery<FakeEntity> MakeQuery(
        string text = "hello",
        Func<SemanticSearchQuery<FakeEntity>, CancellationToken, Task<IReadOnlyList<SemanticDbResult>>>? executor = null)
    {
        executor ??= (_, _) => Task.FromResult<IReadOnlyList<SemanticDbResult>>([]);
        return new SemanticSearchQuery<FakeEntity>(text, executor);
    }

    [Fact]
    public void QueryText_IsSetFromConstructor()
    {
        var query = MakeQuery("blue shoes");
        Assert.Equal("blue shoes", query.QueryText);
    }

    [Fact]
    public void TopK_IsNullByDefault()
    {
        var query = MakeQuery();
        Assert.Null(query.TopK);
    }

    [Fact]
    public void ScopeKey_IsNullByDefault()
    {
        var query = MakeQuery();
        Assert.Null(query.ScopeKey);
    }

    [Fact]
    public void Limit_SetsTopK()
    {
        var query = MakeQuery().Limit(42);
        Assert.Equal(42, query.TopK);
    }

    [Fact]
    public void WithScope_SetsStringRepresentation()
    {
        var guid = Guid.Parse("12345678-0000-0000-0000-000000000000");
        var query = MakeQuery().WithScope(guid);
        Assert.Equal(guid.ToString(), query.ScopeKey);
    }

    [Fact]
    public void WithScope_Null_LeavesNullScopeKey()
    {
        var query = MakeQuery().WithScope(null);
        Assert.Null(query.ScopeKey);
    }

    [Fact]
    public void Limit_Zero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MakeQuery().Limit(0));
    }

    [Fact]
    public void Limit_Negative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MakeQuery().Limit(-1));
    }

    [Fact]
    public void FluentChaining_ReturnsSameInstance()
    {
        var query = MakeQuery();
        var after = query.Limit(5).WithScope("tenant-1");
        Assert.Same(query, after);
    }

    [Fact]
    public async Task ToListAsync_InvokesExecutorWithSelf()
    {
        SemanticSearchQuery<FakeEntity>? captured = null;
        Task<IReadOnlyList<SemanticDbResult>> Executor(SemanticSearchQuery<FakeEntity> q, CancellationToken _)
        {
            captured = q;
            return Task.FromResult<IReadOnlyList<SemanticDbResult>>([]);
        }

        var query = MakeQuery("test", Executor).Limit(7).WithScope("s");
        await query.ToListAsync();

        Assert.Same(query, captured);
        Assert.Equal(7, captured!.TopK);
        Assert.Equal("s", captured.ScopeKey);
    }

    [Fact]
    public void StrategyType_DefaultsToIInMemoryVectorSearch()
    {
        var query = MakeQuery();
        Assert.Equal(typeof(IInMemoryVectorSearch), query.StrategyType);
    }

    [Fact]
    public void UseInMemorySearch_SetsStrategyTypeToIInMemoryVectorSearch()
    {
        var query = MakeQuery().UseInMemorySearch();
        Assert.Equal(typeof(IInMemoryVectorSearch), query.StrategyType);
    }

    [Fact]
    public void WithStrategy_SetsStrategyType()
    {
        var query = MakeQuery().WithStrategy(typeof(IFakeStrategy));
        Assert.Equal(typeof(IFakeStrategy), query.StrategyType);
    }

    [Fact]
    public void WithStrategy_ReturnsChainedInstance()
    {
        var query = MakeQuery();
        var result = query.WithStrategy(typeof(IFakeStrategy));
        Assert.Same(query, result);
    }

    [Fact]
    public void UseInMemorySearch_ReturnsChainedInstance()
    {
        var query = MakeQuery();
        var result = query.UseInMemorySearch();
        Assert.Same(query, result);
    }

    private interface IFakeStrategy { }
    private sealed class FakeEntity : ISearchableEntity { }
}
