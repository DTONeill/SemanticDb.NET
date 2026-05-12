using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;
using SemanticDb.IntegrationTests.Infrastructure;

namespace SemanticDb.IntegrationTests.Outbox;

public sealed class PipelineTests : IntegrationTestBase
{
    [Fact]
    public async Task Insert_ThenProcess_MakesEntitySearchable()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 1, Name = "Inferno", Description = "A fire spell" });
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("fire").ToListAsync();
        Assert.Single(results);
        Assert.Equal("1", results[0].EntityId);
    }

    [Fact]
    public async Task Delete_ThenProcess_RemovesEntityFromSearch()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 2, Name = "Tsunami", Description = "A water spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        db.Products.Remove(product);
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("water").ToListAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task ScopeKey_IsolatesResultsPerTenant()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new TestProduct { Id = 3, Name = "Blaze", Description = "A fire spell", TenantId = "tenant-A" },
            new TestProduct { Id = 4, Name = "Ember", Description = "A fire spell", TenantId = "tenant-B" });
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        var resultsA = await Searcher.Query("fire").WithScope("tenant-A").ToListAsync();
        Assert.Single(resultsA);
        Assert.Equal("3", resultsA[0].EntityId);

        var resultsB = await Searcher.Query("fire").WithScope("tenant-B").ToListAsync();
        Assert.Single(resultsB);
        Assert.Equal("4", resultsB[0].EntityId);
    }

    [Fact]
    public void Query_EmptyText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Searcher.Query(""));
    }

    [Fact]
    public async Task ModifyThenDelete_OverwritesOutboxEntryToPendingDelete()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 10, Name = "Blizzard", Description = "An ice spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync(); // consume the initial Pending entry

        product.Description = "A powerful ice spell";
        await db.SaveChangesAsync(); // creates Pending entry

        db.Products.Remove(product);
        await db.SaveChangesAsync(); // should overwrite to PendingDelete, not add a second row

        var outbox = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "10").ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.PendingDelete, outbox[0].Status);
    }

    [Fact]
    public async Task ModifyThenDelete_WhenProcessed_RemovesEntityFromSearch()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 11, Name = "Frostbolt", Description = "A cold spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        product.Description = "An updated cold spell";
        await db.SaveChangesAsync();

        db.Products.Remove(product);
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("cold").ToListAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task ModifySoftDelete_OverwritesOutboxEntryToPendingDelete()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 21, Name = "Blizzard", Description = "An ice storm" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        product.Description = "A powerful ice storm";
        await db.SaveChangesAsync(); // creates Pending entry

        product.IsDeleted = true;
        await db.SaveChangesAsync(); // should overwrite to PendingDelete, not add a second row

        var outbox = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "21").ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.PendingDelete, outbox[0].Status);
    }

    [Fact]
    public async Task ModifySoftDelete_WhenProcessed_RemovesEntityFromSearch()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 22, Name = "Shatter", Description = "A crystal spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        product.Description = "An updated crystal spell";
        await db.SaveChangesAsync();

        product.IsDeleted = true;
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("crystal").ToListAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task SoftDelete_ThenProcess_RemovesEntityFromSearch()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 20, Name = "Fireball", Description = "A powerful fire spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        product.IsDeleted = true;
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("fire").ToListAsync();
        Assert.Empty(results);
    }
}
