using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;
using SemanticDb.IntegrationTests.Infrastructure;

namespace SemanticDb.IntegrationTests;

public sealed class EfRagOutboxStoreReindexTests : IntegrationTestBase
{
    private const string ChunkName = nameof(ProductChunk);

    [Fact]
    public async Task WithNoEntities_EnqueuesNoOutboxEntries()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();

        await store.EnqueueReindexAsync(ChunkName, typeof(TestProduct));

        await using var db = CreateDbContext();
        var entries = await db.Set<RagOutboxEntry>()
            .Where(e => e.ChunkName == ChunkName)
            .ToListAsync();
        Assert.Empty(entries);
    }

    [Fact]
    public async Task WithEntities_EnqueuesOnePendingEntryPerEntity()
    {
        await using (var db = CreateDbContext())
        {
            db.Products.AddRange(
                new TestProduct { Id = 400, Name = "Alpha", Description = "A fire spell" },
                new TestProduct { Id = 401, Name = "Beta",  Description = "A water spell" },
                new TestProduct { Id = 402, Name = "Gamma", Description = "A nature spell" });
            await db.SaveChangesAsync();
        }

        // Consume the entries created by the interceptor so the assertion is unambiguous.
        await Processor.ProcessPendingAsync();

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();
            await store.EnqueueReindexAsync(ChunkName, typeof(TestProduct));
        }

        await using var db2 = CreateDbContext();
        var entries = await db2.Set<RagOutboxEntry>()
            .Where(e => e.ChunkName == ChunkName)
            .OrderBy(e => e.EntityId)
            .ToListAsync();

        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal(RagOutboxStatus.Pending, e.Status));
        Assert.Equal(["400", "401", "402"], entries.Select(e => e.EntityId).ToArray());
    }

    [Fact]
    public async Task WithEntities_ThenProcess_SearchReturnsResults()
    {
        await using (var db = CreateDbContext())
        {
            db.Products.AddRange(
                new TestProduct { Id = 410, Name = "Inferno", Description = "A fire spell" },
                new TestProduct { Id = 411, Name = "Torrent", Description = "A water spell" });
            await db.SaveChangesAsync();
        }

        await Processor.ProcessPendingAsync();

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();
            await store.EnqueueReindexAsync(ChunkName, typeof(TestProduct));
        }

        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("fire").ToListAsync();
        Assert.Contains(results, r => r.EntityId == "410");
    }
}
