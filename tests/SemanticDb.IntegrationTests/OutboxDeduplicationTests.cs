using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;
using SemanticDb.IntegrationTests.Infrastructure;

namespace SemanticDb.IntegrationTests;

public sealed class OutboxDeduplicationTests : IntegrationTestBase
{
    // Chunk name is derived from the ISearchableEntity implementation type name.
    private const string ChunkName = nameof(ProductChunk);

    [Fact]
    public async Task MultipleModifies_ProducesOnlyOneOutboxEntry()
    {
        await using var db = await CreateDbContextAsync();
        var product = new TestProduct { Id = 20, Name = "Fireball", Description = "A fire spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        product.Description = "A bigger fire spell";
        await db.SaveChangesAsync();

        product.Description = "The biggest fire spell";
        await db.SaveChangesAsync();

        var outbox = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "20").ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
    }

    [Fact]
    public async Task DeleteThenReInsert_OverwritesPendingDeleteToPending()
    {
        await using var db = await CreateDbContextAsync();
        var product = new TestProduct { Id = 21, Name = "Meteor", Description = "A space spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        db.Products.Remove(product);
        await db.SaveChangesAsync(); // PendingDelete

        var reAdded = new TestProduct { Id = 21, Name = "Meteor", Description = "Back from space" };
        db.Products.Add(reAdded);
        await db.SaveChangesAsync(); // should overwrite PendingDelete → Pending

        var outbox = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "21").ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
    }

    [Fact]
    public async Task FailedEntry_IsReplacedByNewOperation()
    {
        await using var db = await CreateDbContextAsync();
        var product = new TestProduct { Id = 22, Name = "Thunder", Description = "A storm spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Simulate a permanently failed entry (no worker owns it — ClaimedBy stays null)
        var entry = await db.Set<RagOutboxEntry>().SingleAsync(e => e.EntityId == "22");
        entry.Status = RagOutboxStatus.Failed;
        entry.Error = "Embedding service unavailable";
        entry.RetryCount = 3;
        await db.SaveChangesAsync();

        product.Description = "A powerful storm spell";
        await db.SaveChangesAsync();

        var outbox = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "22").ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
        Assert.Null(outbox[0].Error);
        Assert.Equal(0, outbox[0].RetryCount);
    }

    [Fact]
    public async Task ProcessingEntry_IsNotOverwritten_NewEntryCreatedAlongside()
    {
        await using var db = await CreateDbContextAsync();
        var product = new TestProduct { Id = 23, Name = "Earthquake", Description = "A ground spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Simulate a worker claiming the entry
        var entry = await db.Set<RagOutboxEntry>().SingleAsync(e => e.EntityId == "23");
        entry.Status = RagOutboxStatus.Processing;
        entry.ClaimedBy = "worker-1";
        entry.ClaimedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // A new modification while the worker holds the claim
        product.Description = "A massive ground spell";
        await db.SaveChangesAsync();

        var outbox = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == "23")
            .ToListAsync();

        Assert.Equal(2, outbox.Count);
        Assert.Contains(outbox, e => e.Status == RagOutboxStatus.Processing && e.ClaimedBy == "worker-1");
        Assert.Contains(outbox, e => e.Status == RagOutboxStatus.Pending && e.ClaimedBy == null);
    }

    [Fact]
    public async Task SoftDelete_OverwritesPendingToPendingDelete()
    {
        await using var db = await CreateDbContextAsync();
        var product = new TestProduct { Id = 30, Name = "Freeze", Description = "An ice spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        product.Description = "An updated ice spell";
        await db.SaveChangesAsync(); // creates Pending entry

        product.IsDeleted = true;
        await db.SaveChangesAsync(); // should overwrite Pending → PendingDelete

        var outbox = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "30").ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.PendingDelete, outbox[0].Status);
    }

    [Fact]
    public async Task SoftDeleteThenRestore_OverwritesPendingDeleteToPending()
    {
        await using var db = await CreateDbContextAsync();
        var product = new TestProduct { Id = 31, Name = "Thaw", Description = "A thaw spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        product.IsDeleted = true;
        await db.SaveChangesAsync(); // PendingDelete

        product.IsDeleted = false;
        await db.SaveChangesAsync(); // should overwrite PendingDelete → Pending

        var outbox = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "31").ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(RagOutboxStatus.Pending, outbox[0].Status);
    }

    [Fact]
    public async Task TwoEntities_EachDeduplicatedIndependently()
    {
        await using var db = await CreateDbContextAsync();
        var p1 = new TestProduct { Id = 24, Name = "Wind", Description = "A wind spell" };
        var p2 = new TestProduct { Id = 25, Name = "Rain", Description = "A rain spell" };
        db.Products.AddRange(p1, p2);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        p1.Description = "A gentle wind spell";
        p2.Description = "A heavy rain spell";
        await db.SaveChangesAsync(); // 2 Pending entries

        db.Products.Remove(p2);
        await db.SaveChangesAsync(); // p2: Pending → PendingDelete; p1 untouched

        var outboxP1 = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "24").ToListAsync();
        var outboxP2 = await db.Set<RagOutboxEntry>().Where(e => e.EntityId == "25").ToListAsync();

        Assert.Single(outboxP1);
        Assert.Equal(RagOutboxStatus.Pending, outboxP1[0].Status);

        Assert.Single(outboxP2);
        Assert.Equal(RagOutboxStatus.PendingDelete, outboxP2[0].Status);
    }
}
