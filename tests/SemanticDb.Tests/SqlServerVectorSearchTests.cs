using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SemanticDb.Core.Configuration;
using SemanticDb.EF.Extensions;
using SemanticDb.EF.SqlServer.Search;

namespace SemanticDb.Tests;

public class SqlServerVectorSearchTests
{
    // ── Score semantics ───────────────────────────────────────────────────────────

    /// <summary>
    /// Regression test for the score semantics bug where SqlServerVectorSearch used
    /// raw VECTOR_DISTANCE (lower = more similar) while InMemoryVectorSearch uses
    /// cosine similarity (higher = more similar). Both providers must use similarity
    /// so Score means the same thing regardless of the active provider.
    /// </summary>
    [Fact]
    public void SqlQueries_UseCosineSimilarity_NotDistance()
    {
        using var db = new VectorSearchTestDbContext(new NoOpInterceptor());
        var sut = new SqlServerVectorSearch(
            db,
            new SemanticDbOptions { VectorDimensions = 3 },
            NullLogger<SqlServerVectorSearch>.Instance);

        var sqlWithoutScope = GetSql(sut, "_sqlWithoutScope");
        var sqlWithScope    = GetSql(sut, "_sqlWithScope");

        // Must compute similarity (1 - distance), not raw distance
        Assert.Contains("1.0 - VECTOR_DISTANCE", sqlWithoutScope);
        Assert.Contains("1.0 - VECTOR_DISTANCE", sqlWithScope);

        // Must rank highest similarity first
        Assert.Contains("ORDER BY Score DESC", sqlWithoutScope);
        Assert.Contains("ORDER BY Score DESC", sqlWithScope);
    }

    private static string GetSql(SqlServerVectorSearch sut, string fieldName) =>
        (string)typeof(SqlServerVectorSearch)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(sut)!;

    // ── Connection lifecycle ──────────────────────────────────────────────────────

    /// <summary>
    /// Regression test for the connection leak bug where SearchAsync called
    /// OpenConnectionAsync on the DbContext's managed connection and never closed it.
    /// The fix creates its own SqlConnection so EF's connection is never touched.
    /// </summary>
    [Fact]
    public async Task SearchAsync_DoesNotOpenDbContextManagedConnection()
    {
        var tracker = new ConnectionOpenTracker();
        await using var db = new VectorSearchTestDbContext(tracker);
        var sut = new SqlServerVectorSearch(
            db,
            new SemanticDbOptions { VectorDimensions = 2 },
            NullLogger<SqlServerVectorSearch>.Instance);

        // Will fail to connect to the unreachable server — that is expected and irrelevant.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.SearchAsync("chunk", [1f, 0f], null, 5, CancellationToken.None));

        Assert.False(tracker.WasConnectionOpenAttempted,
            "SearchAsync must not open the DbContext's managed connection; it must own its own SqlConnection.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private sealed class NoOpInterceptor : DbConnectionInterceptor { }

    private sealed class ConnectionOpenTracker : DbConnectionInterceptor
    {
        public bool WasConnectionOpenAttempted { get; private set; }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            WasConnectionOpenAttempted = true;
            return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
        }
    }

    private sealed class VectorSearchTestDbContext : DbContext
    {
        private readonly IInterceptor _interceptor;

        public VectorSearchTestDbContext(IInterceptor interceptor)
        {
            _interceptor = interceptor;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder
                .UseSqlServer(
                    "Server=invalid-host-does-not-exist;Database=SemanticDbTest;Connect Timeout=1;Encrypt=false;")
                .AddInterceptors(_interceptor);

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.ApplySemanticDbConfiguration();
    }
}
