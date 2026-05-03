using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SemanticDb.Core.Configuration;
using SemanticDb.EF.Extensions;
using SemanticDb.EF.SqlServer.Search;

namespace SemanticDb.Tests;

public class SqlServerVectorSearchTests
{
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
