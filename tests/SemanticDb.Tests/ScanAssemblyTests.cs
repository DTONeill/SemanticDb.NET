using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Extensions;

namespace SemanticDb.Tests;

public class ScanAssemblyTests
{
    private readonly SearchableEntityRegistry _registry;

    public ScanAssemblyTests()
    {
        var services = new ServiceCollection();
        services.AddSemanticDb(typeof(ScanAssemblyTests).Assembly);
        _registry = services.BuildServiceProvider().GetRequiredService<SearchableEntityRegistry>();
    }

    [Fact]
    public void ScanAssembly_DiscoversTwoParamImplementation()
    {
        Assert.True(_registry.TryGetByChunkName(nameof(StringScopedSearchable), out _));
    }

    [Fact]
    public void ScanAssembly_SetsEntityType_ToFirstTypeParameter()
    {
        _registry.TryGetByChunkName(nameof(StringScopedSearchable), out var reg);
        Assert.Equal(typeof(ScanEntity), reg!.EntityType);
    }

    [Fact]
    public void ScanAssembly_WiresToSearchContent()
    {
        _registry.TryGetByChunkName(nameof(StringScopedSearchable), out var reg);
        Assert.Equal("content-7", reg!.ToSearchContent(new ScanEntity { Id = 7 }));
    }

    [Fact]
    public void ScanAssembly_WiresToPromptContext_WhenOverridden()
    {
        _registry.TryGetByChunkName(nameof(StringScopedSearchable), out var reg);
        Assert.Equal("context-7", reg!.ToPromptContext(new ScanEntity { Id = 7 }));
    }

    [Fact]
    public void ScanAssembly_WiresToPromptContext_DefaultsToSearchContent_WhenNotOverridden()
    {
        _registry.TryGetByChunkName(nameof(IntScopedSearchable), out var reg);
        var entity = new ScanEntity { Id = 3 };
        Assert.Equal(reg!.ToSearchContent(entity), reg.ToPromptContext(entity));
    }

    [Fact]
    public void ScanAssembly_WiresVersion()
    {
        _registry.TryGetByChunkName(nameof(StringScopedSearchable), out var reg);
        Assert.Equal(2, reg!.Version);
    }

    [Fact]
    public void ScanAssembly_WiresGetScopeKey_ForStringKey()
    {
        _registry.TryGetByChunkName(nameof(StringScopedSearchable), out var reg);
        Assert.Equal("scope-5", reg!.GetScopeKey(new ScanEntity { Id = 5 }));
    }

    [Fact]
    public void ScanAssembly_WiresGetScopeKey_ForValueTypeKey()
    {
        _registry.TryGetByChunkName(nameof(IntScopedSearchable), out var reg);
        Assert.Equal(42, reg!.GetScopeKey(new ScanEntity { Id = 42 }));
    }

    [Fact]
    public void ScanAssembly_WiresGetScopeKey_ReturnsNull_WhenNotOverridden()
    {
        _registry.TryGetByChunkName(nameof(DefaultScopeSearchable), out var reg);
        Assert.Null(reg!.GetScopeKey(new ScanEntity()));
    }

    [Fact]
    public void ScanAssembly_WiresIsDeleted_ReturnsFalseByDefault()
    {
        _registry.TryGetByChunkName(nameof(DefaultScopeSearchable), out var reg);
        Assert.False(reg!.IsDeleted(new ScanEntity()));
    }

    [Fact]
    public void ScanAssembly_WiresIsDeleted_WhenOverridden()
    {
        _registry.TryGetByChunkName(nameof(SoftDeletableSearchable), out var reg);
        Assert.True(reg!.IsDeleted(new ScanEntity { IsDeleted = true }));
        Assert.False(reg!.IsDeleted(new ScanEntity { IsDeleted = false }));
    }

    // ── Test entities ────────────────────────────────────────────────────────

    public sealed class ScanEntity
    {
        public int Id { get; init; }
        public bool IsDeleted { get; init; }
    }

    public sealed class StringScopedSearchable : ISearchableEntity<ScanEntity, string>
    {
        public string ToSearchContent(ScanEntity e) => $"content-{e.Id}";
        public string ToPromptContext(ScanEntity e) => $"context-{e.Id}";
        public object? GetScopeKey(ScanEntity e) => $"scope-{e.Id}";
        public int Version => 2;
    }

    public sealed class IntScopedSearchable : ISearchableEntity<ScanEntity, int>
    {
        public string ToSearchContent(ScanEntity e) => $"content-{e.Id}";
        public object? GetScopeKey(ScanEntity e) => e.Id;
    }

    public sealed class DefaultScopeSearchable : ISearchableEntity<ScanEntity, object>
    {
        public string ToSearchContent(ScanEntity e) => $"content-{e.Id}";
        // GetScopeKey and IsDeleted not overridden — use interface defaults
    }

    public sealed class SoftDeletableSearchable : ISearchableEntity<ScanEntity, object>
    {
        public string ToSearchContent(ScanEntity e) => $"content-{e.Id}";
        public bool IsDeleted(ScanEntity e) => e.IsDeleted;
    }
}
