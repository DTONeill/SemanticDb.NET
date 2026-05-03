using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SemanticDb.EF.SqlServer.Hosting;

namespace SemanticDb.Tests;

public class SqlServerStartupServicesTests
{
    [Fact]
    public async Task SqlServerVectorValidationService_StartAsync_DoesNotOpenDbContextManagedConnection()
    {
        var (scopeFactory, tracker) = CreateScopeFactory();
        var sut = new SqlServerVectorValidationService(
            scopeFactory,
            NullLogger<SqlServerVectorValidationService>.Instance);

        // Will fail to connect to the unreachable server — that is expected and irrelevant.
        await Assert.ThrowsAnyAsync<Exception>(() => sut.StartAsync(CancellationToken.None));

        Assert.False(tracker.WasConnectionOpenAttempted,
            "StartAsync must not open the DbContext's managed connection; it must own its own SqlConnection.");
    }

    [Fact]
    public async Task SqlServerVectorFeatureConfigurator_StartAsync_DoesNotOpenDbContextManagedConnection()
    {
        var (scopeFactory, tracker) = CreateScopeFactory();
        var sut = new SqlServerVectorFeatureConfigurator(
            scopeFactory,
            NullLogger<SqlServerVectorFeatureConfigurator>.Instance);

        // Connection errors are intentionally swallowed by this service — must not throw.
        await sut.StartAsync(CancellationToken.None);

        Assert.False(tracker.WasConnectionOpenAttempted,
            "StartAsync must not open the DbContext's managed connection; it must own its own SqlConnection.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static (IServiceScopeFactory scopeFactory, ConnectionOpenTracker tracker) CreateScopeFactory()
    {
        var tracker = new ConnectionOpenTracker();
        var services = new ServiceCollection();
        services.AddDbContext<StartupTestDbContext>(opt =>
            opt.UseSqlServer(
                    "Server=invalid-host-does-not-exist;Database=SemanticDbTest;Connect Timeout=1;Encrypt=false;")
               .AddInterceptors(tracker));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<StartupTestDbContext>());
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IServiceScopeFactory>(), tracker);
    }

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

    private sealed class StartupTestDbContext : DbContext
    {
        public StartupTestDbContext(DbContextOptions<StartupTestDbContext> options) : base(options) { }
    }
}
