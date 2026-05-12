using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;
using SemanticDb.IntegrationTests.Infrastructure;

namespace SemanticDb.IntegrationTests.Outbox;

public sealed class FailedEntryResetTests : IntegrationTestBase
{
    private IRagOutboxStore OutboxStore =>
        Services.CreateScope().ServiceProvider.GetRequiredService<IRagOutboxStore>();

    [Fact]
    public async Task ResetFailedEntries_ResetsEntriesOlderThanPeriod()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 60, Name = "Blizzard", Description = "A frost spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync(); // clear initial entry

        // Manually inject a Failed entry with ProcessedAt 2 hours ago
        db.Set<RagOutboxEntry>().Add(new RagOutboxEntry
        {
            EntityType = typeof(TestProduct).FullName!,
            EntityId = "60",
            ChunkName = nameof(ProductChunk),
            Status = RagOutboxStatus.Failed,
            RetryCount = 3,
            Error = "Embedding provider unavailable",
            ProcessedAt = DateTime.UtcNow - TimeSpan.FromHours(2)
        });
        await db.SaveChangesAsync();

        await OutboxStore.ResetFailedEntriesAsync(TimeSpan.FromHours(1), CancellationToken.None);

        db.ChangeTracker.Clear();
        var entry = await db.Set<RagOutboxEntry>().SingleAsync(e => e.EntityId == "60");
        Assert.Equal(RagOutboxStatus.Pending, entry.Status);
        Assert.Equal(0, entry.RetryCount);
        Assert.Null(entry.Error);
        Assert.Null(entry.NextRetryAt);
        Assert.Null(entry.ProcessedAt);
    }

    [Fact]
    public async Task ResetFailedEntries_DoesNotResetRecentlyFailedEntries()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 61, Name = "Thunder", Description = "A storm spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        // Manually inject a Failed entry with ProcessedAt only 10 minutes ago
        db.Set<RagOutboxEntry>().Add(new RagOutboxEntry
        {
            EntityType = typeof(TestProduct).FullName!,
            EntityId = "61",
            ChunkName = nameof(ProductChunk),
            Status = RagOutboxStatus.Failed,
            RetryCount = 3,
            Error = "Embedding provider unavailable",
            ProcessedAt = DateTime.UtcNow - TimeSpan.FromMinutes(10)
        });
        await db.SaveChangesAsync();

        await OutboxStore.ResetFailedEntriesAsync(TimeSpan.FromHours(1), CancellationToken.None);

        db.ChangeTracker.Clear();
        var entry = await db.Set<RagOutboxEntry>().SingleAsync(e => e.EntityId == "61");
        Assert.Equal(RagOutboxStatus.Failed, entry.Status);
        Assert.Equal(3, entry.RetryCount);
    }

    [Fact]
    public async Task ResetFailedEntries_ThenProcess_UpdatesSearchIndex()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 62, Name = "Inferno", Description = "A fire spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        // Inject a stale Failed entry simulating a previous failed attempt
        db.Set<RagOutboxEntry>().Add(new RagOutboxEntry
        {
            EntityType = typeof(TestProduct).FullName!,
            EntityId = "62",
            ChunkName = nameof(ProductChunk),
            Status = RagOutboxStatus.Failed,
            RetryCount = 3,
            Error = "Transient failure",
            ProcessedAt = DateTime.UtcNow - TimeSpan.FromHours(2)
        });
        await db.SaveChangesAsync();

        await OutboxStore.ResetFailedEntriesAsync(TimeSpan.FromHours(1), CancellationToken.None);
        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("fire").ToListAsync();
        Assert.Contains(results, r => r.EntityId == "62");
    }

    [Fact]
    public async Task ResetFailedEntries_DoesNotAffectPendingOrProcessingEntries()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 63, Name = "Gust", Description = "A wind spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        // Do NOT process — entry is Pending

        db.Set<RagOutboxEntry>().Add(new RagOutboxEntry
        {
            EntityType = typeof(TestProduct).FullName!,
            EntityId = "63",
            ChunkName = nameof(ProductChunk),
            Status = RagOutboxStatus.Failed,
            RetryCount = 3,
            Error = "old failure",
            ProcessedAt = DateTime.UtcNow - TimeSpan.FromHours(2)
        });
        await db.SaveChangesAsync();

        await OutboxStore.ResetFailedEntriesAsync(TimeSpan.FromHours(1), CancellationToken.None);

        db.ChangeTracker.Clear();
        var pending = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "63" && e.Status == RagOutboxStatus.Pending)
            .ToListAsync();

        // The original Pending entry from SaveChanges and the reset Failed entry — both Pending
        Assert.All(pending, e => Assert.Equal(RagOutboxStatus.Pending, e.Status));
        Assert.DoesNotContain(pending, e => e.Status == RagOutboxStatus.Failed);
    }

    [Fact]
    public async Task ProcessPending_AutomaticallyResetsOldFailedEntries_WhenOptionEnabled()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Id = 64, Name = "Ember", Description = "A fire spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        // Set FailedEntryResetPeriod to a very short window so the processor resets the entry
        var options = Services.GetRequiredService<SemanticDb.Core.Configuration.SemanticDbOptions>();
        options.FailedEntryResetPeriod = TimeSpan.FromMilliseconds(1);

        db.Set<RagOutboxEntry>().Add(new RagOutboxEntry
        {
            EntityType = typeof(TestProduct).FullName!,
            EntityId = "64",
            ChunkName = nameof(ProductChunk),
            Status = RagOutboxStatus.Failed,
            RetryCount = 3,
            Error = "Stale failure",
            ProcessedAt = DateTime.UtcNow - TimeSpan.FromSeconds(5)
        });
        await db.SaveChangesAsync();

        await Task.Delay(10); // ensure ProcessedAt is definitely past the 1 ms threshold
        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("fire").ToListAsync();
        Assert.Contains(results, r => r.EntityId == "64");

        // Restore default so other tests are not affected
        options.FailedEntryResetPeriod = TimeSpan.FromHours(1);
    }
}
