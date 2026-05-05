using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;
using SemanticDb.IntegrationTests.Infrastructure;

namespace SemanticDb.IntegrationTests;

public sealed class ReindexTests : IntegrationTestBase
{
    private ISemanticDbIndexer Indexer =>
        Services.CreateScope().ServiceProvider.GetRequiredService<ISemanticDbIndexer>();

    [Fact]
    public async Task RequestReindex_CreatesOutboxEntry()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 20, Name = "Fireball", Description = "A fire spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync(); // clear the initial entry

        // Simulate an out-of-band modification (bypass EF interceptor)
        await db.Products
            .Where(p => p.Id == 20)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Description, "An upgraded fire spell"));

        await Indexer.RequestReindexAsync(product);

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "20")
            .ToListAsync();

        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
    }

    [Fact]
    public async Task RequestReindex_ThenProcess_UpdatesSearchIndex()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 21, Name = "Aqua", Description = "A water spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await db.Products
            .Where(p => p.Id == 21)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Description, "A fire spell"));

        // Reload to get updated state for the indexer
        product.Description = "A fire spell";
        await Indexer.RequestReindexAsync(product);
        await Processor.ProcessPendingAsync();

        var fireResults = await SearchService.SearchAsync<ProductChunk>("fire");
        var updated = fireResults.SingleOrDefault(r => r.EntityId == "21");

        Assert.NotNull(updated);
        Assert.Equal("Aqua: A fire spell", updated.PromptContext);
    }

    [Fact]
    public async Task RequestReindex_IsIdempotent_NosDuplicateOutboxEntry()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 22, Name = "Storm", Description = "A nature spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAsync(product);
        await Indexer.RequestReindexAsync(product);

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "22")
            .ToListAsync();

        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
    }

    [Fact]
    public async Task RequestReindex_ResetsFailedEntry()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 23, Name = "Thunder", Description = "A fire spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Manually mark the outbox entry as failed
        var entry = await db.Set<RagOutboxEntry>().SingleAsync(e => e.EntityId == "23");
        entry.Status = RagOutboxStatus.Failed;
        entry.RetryCount = 3;
        entry.Error = "Embedding provider unavailable";
        await db.SaveChangesAsync();

        await Indexer.RequestReindexAsync(product);

        db.ChangeTracker.Clear();
        var outbox = await db.Set<RagOutboxEntry>().SingleAsync(e => e.EntityId == "23");
        Assert.Equal(RagOutboxStatus.Pending, outbox.Status);
        Assert.Equal(0, outbox.RetryCount);
        Assert.Null(outbox.Error);
    }

    [Fact]
    public async Task RequestReindex_UnregisteredEntity_Throws()
    {
        var unregistered = new UnregisteredEntity();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Indexer.RequestReindexAsync(unregistered));
    }

    private sealed class UnregisteredEntity
    {
    }
}

public sealed class ReindexByKeyTests : IntegrationTestBase
{
    private ISemanticDbIndexer Indexer =>
        Services.CreateScope().ServiceProvider.GetRequiredService<ISemanticDbIndexer>();

    [Fact]
    public async Task RequestReindexByKey_CreatesOutboxEntry()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 40, Name = "Vortex", Description = "A wind spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAsync<TestProduct>(40);

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "40")
            .ToListAsync();

        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
    }

    [Fact]
    public async Task RequestReindexByKey_ThenProcess_UpdatesSearchIndex()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 41, Name = "Blaze", Description = "A water spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await db.Products
            .Where(p => p.Id == 41)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Description, "A fire spell"));

        await Indexer.RequestReindexAsync<TestProduct>(41);
        await Processor.ProcessPendingAsync();

        var results = await SearchService.SearchAsync<ProductChunk>("fire");
        Assert.Contains(results, r => r.EntityId == "41");
    }

    [Fact]
    public async Task RequestReindexByKey_IsIdempotent_NoDuplicateOutboxEntry()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 42, Name = "Gale", Description = "A wind spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAsync<TestProduct>(42);
        await Indexer.RequestReindexAsync<TestProduct>(42);

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "42")
            .ToListAsync();

        Assert.Single(outbox);
    }

    [Fact]
    public async Task RequestReindexByKey_UnregisteredEntity_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Indexer.RequestReindexAsync<UnregisteredEntity>(1));
    }

    private sealed class UnregisteredEntity
    {
    }
}

public sealed class BulkReindexTests : IntegrationTestBase
{
    private ISemanticDbIndexer Indexer =>
        Services.CreateScope().ServiceProvider.GetRequiredService<ISemanticDbIndexer>();

    [Fact]
    public async Task RequestReindexAll_CreatesOutboxEntryPerEntity()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new TestProduct { Id = 30, Name = "Alpha", Description = "First" },
            new TestProduct { Id = 31, Name = "Beta", Description = "Second" },
            new TestProduct { Id = 32, Name = "Gamma", Description = "Third" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAsync<TestProduct>();

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "30" || e.EntityId == "31" || e.EntityId == "32")
            .ToListAsync();

        Assert.Equal(3, outbox.Count);
        Assert.All(outbox, e => Assert.Equal(RagOutboxStatus.Pending, e.Status));
    }

    [Fact]
    public async Task RequestReindexAll_EmptyTable_CreatesNoOutboxEntries()
    {
        await using var db = CreateDbContext();

        await Indexer.RequestReindexAsync<TestProduct>();

        var outbox = await db.Set<RagOutboxEntry>().ToListAsync();
        Assert.Empty(outbox);
    }

    [Fact]
    public async Task RequestReindexAll_IsIdempotent_NoDuplicateOutboxEntries()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 33, Name = "Delta", Description = "Fourth" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAsync<TestProduct>();
        await Indexer.RequestReindexAsync<TestProduct>();

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "33")
            .ToListAsync();

        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
    }

    [Fact]
    public async Task RequestReindexAll_ThenProcess_IndexesAllEntities()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new TestProduct { Id = 34, Name = "Epsilon", Description = "A lightning spell" },
            new TestProduct { Id = 35, Name = "Zeta", Description = "A lightning spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAsync<TestProduct>();
        await Processor.ProcessPendingAsync();

        var results = await SearchService.SearchAsync<ProductChunk>("lightning");
        var ids = results.Select(r => r.EntityId).ToHashSet();

        Assert.Contains("34", ids);
        Assert.Contains("35", ids);
    }

    [Fact]
    public async Task RequestReindexAll_ReenqueuesAlreadyProcessedEntities()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 36, Name = "Eta", Description = "A frost spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAsync<TestProduct>();

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "36")
            .ToListAsync();

        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
    }

    [Fact]
    public async Task RequestReindexAll_UnregisteredEntity_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Indexer.RequestReindexAsync<UnregisteredEntity>());
    }

    private sealed class UnregisteredEntity
    {
    }
}

public sealed class ReindexAllTests : IntegrationTestBase
{
    private ISemanticDbIndexer Indexer =>
        Services.CreateScope().ServiceProvider.GetRequiredService<ISemanticDbIndexer>();

    [Fact]
    public async Task RequestReindexAll_CreatesOutboxEntriesForEveryRegisteredType()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new TestProduct { Id = 50, Name = "Alpha", Description = "A fire spell" },
            new TestProduct { Id = 51, Name = "Beta",  Description = "A water spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAllAsync();

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "50" || e.EntityId == "51")
            .ToListAsync();

        Assert.Equal(2, outbox.Count);
        Assert.All(outbox, e => Assert.Equal(RagOutboxStatus.Pending, e.Status));
    }

    [Fact]
    public async Task RequestReindexAll_EmptyTable_CreatesNoOutboxEntries()
    {
        await Indexer.RequestReindexAllAsync();

        await using var db = CreateDbContext();
        var outbox = await db.Set<RagOutboxEntry>().ToListAsync();
        Assert.Empty(outbox);
    }

    [Fact]
    public async Task RequestReindexAll_IsIdempotent_NoDuplicateOutboxEntries()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Id = 52, Name = "Gamma", Description = "A nature spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAllAsync();
        await Indexer.RequestReindexAllAsync();

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "52")
            .ToListAsync();

        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
    }

    [Fact]
    public async Task RequestReindexAll_ThenProcess_IndexesAllEntities()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new TestProduct { Id = 53, Name = "Delta",   Description = "A fire spell" },
            new TestProduct { Id = 54, Name = "Epsilon", Description = "A water spell" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        await Indexer.RequestReindexAllAsync();
        await Processor.ProcessPendingAsync();

        var fireResults  = await SearchService.SearchAsync<ProductChunk>("fire");
        var waterResults = await SearchService.SearchAsync<ProductChunk>("water");

        Assert.Contains(fireResults,  r => r.EntityId == "53");
        Assert.Contains(waterResults, r => r.EntityId == "54");
    }
}
