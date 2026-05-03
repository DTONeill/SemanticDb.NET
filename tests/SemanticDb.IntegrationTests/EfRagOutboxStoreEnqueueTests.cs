using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;
using SemanticDb.IntegrationTests.Infrastructure;

namespace SemanticDb.IntegrationTests;

public sealed class EfRagOutboxStoreEnqueueTests : IntegrationTestBase
{
    private const string ChunkName = nameof(ProductChunk);
    private static readonly string EntityType = typeof(TestProduct).FullName!;

    [Fact]
    public async Task NoExistingEntry_InsertsNewPendingEntry()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();

        await store.EnqueueEntityReindexAsync(ChunkName, EntityType, "200");

        await using var db = await CreateDbContextAsync();
        var entries = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "200").ToListAsync();
        Assert.Single(entries);
        Assert.Equal(RagOutboxStatus.Pending, entries[0].Status);
        Assert.Null(entries[0].ClaimedBy);
    }

    [Fact]
    public async Task UnclaimedEntry_ResetsAllFields_NoNewRowCreated()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            db.Set<RagOutboxEntry>().Add(new RagOutboxEntry
            {
                EntityType = EntityType,
                EntityId = "201",
                ChunkName = ChunkName,
                Status = RagOutboxStatus.Failed,
                Error = "embedding unavailable",
                RetryCount = 3,
                NextRetryAt = DateTime.UtcNow.AddMinutes(10),
                ProcessedAt = DateTime.UtcNow.AddMinutes(-5),
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();
            await store.EnqueueEntityReindexAsync(ChunkName, EntityType, "201");
        }

        await using var db2 = await CreateDbContextAsync();
        var entries = await db2.Set<RagOutboxEntry>().Where(e => e.EntityId == "201").ToListAsync();
        Assert.Single(entries);
        var e = entries[0];
        Assert.Equal(RagOutboxStatus.Pending, e.Status);
        Assert.Null(e.Error);
        Assert.Equal(0, e.RetryCount);
        Assert.Null(e.NextRetryAt);
        Assert.Null(e.ProcessedAt);
        Assert.Null(e.ClaimedBy);
        Assert.Null(e.ClaimedAt);
    }

    [Fact]
    public async Task ClaimedEntry_IsPreserved_NewPendingEntryInserted()
    {
        Guid claimedId;
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var entry = new RagOutboxEntry
            {
                EntityType = EntityType,
                EntityId = "202",
                ChunkName = ChunkName,
                Status = RagOutboxStatus.Processing,
                ClaimedBy = "worker-1",
                ClaimedAt = DateTime.UtcNow,
            };
            db.Set<RagOutboxEntry>().Add(entry);
            await db.SaveChangesAsync();
            claimedId = entry.Id;
        }

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();
            await store.EnqueueEntityReindexAsync(ChunkName, EntityType, "202");
        }

        await using var db2 = await CreateDbContextAsync();
        var entries = await db2.Set<RagOutboxEntry>().Where(e => e.EntityId == "202").ToListAsync();
        Assert.Equal(2, entries.Count);

        var claimed = entries.Single(e => e.Id == claimedId);
        Assert.Equal(RagOutboxStatus.Processing, claimed.Status);
        Assert.Equal("worker-1", claimed.ClaimedBy);

        var pending = entries.Single(e => e.Id != claimedId);
        Assert.Equal(RagOutboxStatus.Pending, pending.Status);
        Assert.Null(pending.ClaimedBy);
    }

    [Fact]
    public async Task CalledTwice_SecondCallResetsFirst_NoNewRow()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();
            await store.EnqueueEntityReindexAsync(ChunkName, EntityType, "203");
        }

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();
            await store.EnqueueEntityReindexAsync(ChunkName, EntityType, "203");
        }

        await using var db = await CreateDbContextAsync();
        var entries = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "203").ToListAsync();
        Assert.Single(entries);
        Assert.Equal(RagOutboxStatus.Pending, entries[0].Status);
    }
}
