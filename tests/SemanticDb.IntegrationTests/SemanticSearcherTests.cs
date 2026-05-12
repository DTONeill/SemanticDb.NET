using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Search;
using SemanticDb.IntegrationTests.Infrastructure;

namespace SemanticDb.IntegrationTests;

public sealed class SemanticSearcherTests : IntegrationTestBase
{
    [Fact]
    public void ISemanticSearcher_IsAutoRegistered_ForScannedEntity()
    {
        // Should resolve without explicit registration — auto-registered during assembly scan.
        var searcher = Searcher;
        Assert.NotNull(searcher);
    }

    [Fact]
    public async Task Query_ToListAsync_ReturnsMatchingResult()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 100, Name = "Lightning", Description = "A lightning spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("lightning").ToListAsync();

        Assert.Single(results);
        Assert.Equal("100", results[0].EntityId);
    }

    [Fact]
    public async Task Query_ToListAsync_ReturnsExpectedEntity()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 101, Name = "Frost Nova", Description = "A frost spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("frost").ToListAsync();

        Assert.Single(results);
        Assert.Equal("101", results[0].EntityId);
    }

    [Fact]
    public async Task Query_WithScope_FiltersResultsByScope()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new TestProduct { Id = 102, Name = "Wave", Description = "A water spell", TenantId = "alpha" },
            new TestProduct { Id = 103, Name = "Surge", Description = "A water spell", TenantId = "beta" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("water").WithScope("alpha").ToListAsync();

        Assert.Single(results);
        Assert.Equal("102", results[0].EntityId);
    }

    [Fact]
    public async Task Query_Limit_CappsResultCount()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new TestProduct { Id = 104, Name = "Spark", Description = "An electric spell" },
            new TestProduct { Id = 105, Name = "Bolt", Description = "An electric spell" },
            new TestProduct { Id = 106, Name = "Zap", Description = "An electric spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("electric").Limit(1).ToListAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Query_UseInMemorySearch_ExplicitlySet_ReturnsSameResultAsDefault()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 107, Name = "Blizzard", Description = "A blizzard spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        var defaultResults  = await Searcher.Query("blizzard").ToListAsync();
        var explicitResults = await Searcher.Query("blizzard").UseInMemorySearch().ToListAsync();

        Assert.Equal(defaultResults.Count, explicitResults.Count);
        Assert.Equal(defaultResults[0].EntityId, explicitResults[0].EntityId);
        Assert.Equal(defaultResults[0].Score,    explicitResults[0].Score);
    }
}
