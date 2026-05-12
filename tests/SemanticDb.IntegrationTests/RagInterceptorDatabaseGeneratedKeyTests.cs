using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;
using SemanticDb.IntegrationTests.Infrastructure;

namespace SemanticDb.IntegrationTests;

/// <summary>
/// Verifies that the RagInterceptor correctly handles entities whose primary key is
/// assigned by the database (auto-increment int) rather than by the caller.
///
/// Before the fix, the interceptor captured entity IDs in SavingChangesAsync — before
/// the INSERT — so outbox entries received EF Core's temporary negative placeholder IDs
/// instead of the real database-assigned values. The processor would then fail to reload
/// the entity using that negative ID.
/// </summary>
public sealed class RagInterceptorDatabaseGeneratedKeyTests : IntegrationTestBase
{
    [Fact]
    public async Task Insert_WithDatabaseGeneratedKey_OutboxEntryHasPositiveId()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Name = "Inferno", Description = "A fire spell" });
        await db.SaveChangesAsync();

        var entries = await db.Set<RagOutboxEntry>().ToListAsync();

        var entry = Assert.Single(entries);
        Assert.True(
            int.TryParse(entry.EntityId, out var id) && id > 0,
            $"Expected a positive database-assigned ID but got '{entry.EntityId}'.");
    }

    [Fact]
    public async Task Insert_WithDatabaseGeneratedKey_EntityIsSearchableAfterProcessing()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new TestProduct { Name = "Tsunami", Description = "A water spell" });
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("water").ToListAsync();
        Assert.Single(results);
        Assert.True(int.TryParse(results[0].EntityId, out var id) && id > 0);
    }

    [Fact]
    public async Task Insert_MultipleWithDatabaseGeneratedKeys_AllOutboxEntriesHavePositiveIds()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new TestProduct { Name = "Fireball", Description = "A fire spell" },
            new TestProduct { Name = "Waterfall", Description = "A water spell" },
            new TestProduct { Name = "Thornwall", Description = "A nature spell" });
        await db.SaveChangesAsync();

        var entries = await db.Set<RagOutboxEntry>().ToListAsync();

        Assert.Equal(3, entries.Count);
        Assert.All(entries, e =>
            Assert.True(
                int.TryParse(e.EntityId, out var id) && id > 0,
                $"Expected a positive ID but got '{e.EntityId}'."));

        // All three IDs must be distinct.
        var ids = entries.Select(e => e.EntityId).Distinct().ToList();
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task Insert_MultipleWithDatabaseGeneratedKeys_AllSearchableAfterProcessing()
    {
        await using var db = CreateDbContext();
        var blaze  = new TestProduct { Name = "Blaze",  Description = "A fire spell" };
        var rapids = new TestProduct { Name = "Rapids", Description = "A water spell" };
        db.Products.AddRange(blaze, rapids);
        await db.SaveChangesAsync();

        await Processor.ProcessPendingAsync();

        // The in-memory vector search returns all indexed chunks ranked by score.
        // Assert the top result for each query matches the right entity.
        var fireResults  = await Searcher.Query("fire").ToListAsync();
        var waterResults = await Searcher.Query("water").ToListAsync();

        Assert.Equal(blaze.Id.ToString(),  fireResults[0].EntityId);
        Assert.Equal(rapids.Id.ToString(), waterResults[0].EntityId);
    }

    [Fact]
    public async Task Insert_WithDatabaseGeneratedKey_EntityIdMatchesActualDatabaseId()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Name = "Ember", Description = "A fire spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // After SaveChanges, EF Core has updated product.Id with the real DB-assigned value.
        var entry = await db.Set<RagOutboxEntry>().SingleAsync();
        Assert.Equal(product.Id.ToString(), entry.EntityId);
    }

    [Fact]
    public async Task Insert_SoftDeletedWithDatabaseGeneratedKey_OutboxEntryIsPendingDelete()
    {
        await using var db = CreateDbContext();
        // Create an entity that is already soft-deleted at insertion time.
        db.Products.Add(new TestProduct { Name = "Ashfall", Description = "A fire spell", IsDeleted = true });
        await db.SaveChangesAsync();

        var entry = await db.Set<RagOutboxEntry>().SingleAsync();
        Assert.True(int.TryParse(entry.EntityId, out var id) && id > 0);
        Assert.Equal(RagOutboxStatus.PendingDelete, entry.Status);
    }

    [Fact]
    public async Task Insert_WithDatabaseGeneratedKey_ThenModify_UsesRealIdForBothEntries()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Name = "Geyser", Description = "A water spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        product.Description = "A powerful water spell";
        await db.SaveChangesAsync();

        var entries = await db.Set<RagOutboxEntry>()
            .Where(e => e.EntityId == product.Id.ToString())
            .ToListAsync();

        var entry = Assert.Single(entries);
        Assert.Equal(RagOutboxStatus.Pending, entry.Status);
        Assert.True(int.TryParse(entry.EntityId, out var id) && id > 0);
    }

    [Fact]
    public async Task Insert_WithDatabaseGeneratedKey_ThenSoftDelete_EntityRemovedFromSearch()
    {
        await using var db = CreateDbContext();
        var product = new TestProduct { Name = "Seedling", Description = "A nature spell" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        product.IsDeleted = true;
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        var results = await Searcher.Query("nature").ToListAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task Insert_WithDatabaseGeneratedKey_ScopedSearch_RespectsScope()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new TestProduct { Name = "Flame",  Description = "A fire spell",  TenantId = "tenant-A" },
            new TestProduct { Name = "Ignite", Description = "A fire spell",  TenantId = "tenant-B" });
        await db.SaveChangesAsync();
        await Processor.ProcessPendingAsync();

        var resultsA = await Searcher.Query("fire").WithScope("tenant-A").ToListAsync();
        var resultsB = await Searcher.Query("fire").WithScope("tenant-B").ToListAsync();

        Assert.Single(resultsA);
        Assert.Single(resultsB);
        Assert.NotEqual(resultsA[0].EntityId, resultsB[0].EntityId);
    }
}
