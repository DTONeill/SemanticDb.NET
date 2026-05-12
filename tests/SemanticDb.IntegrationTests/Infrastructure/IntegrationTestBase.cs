using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Extensions;
using SemanticDb.EF.Extensions;
using Xunit;
using SemanticDb.Core.Search;

namespace SemanticDb.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _provider = null!;

    protected IServiceProvider Services => _provider;

    public async Task InitializeAsync()
    {
        // Keep connection open so the in-memory database persists across scoped DbContext instances.
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();

        services
            .AddSemanticDb(typeof(ProductChunk).Assembly)
            .UseEmbeddingsProvider(new FakeEmbeddingGenerator())
            .UseEfCore<TestDbContext>();

        services.AddDbContext<TestDbContext>((sp, opt) =>
            opt.UseSqlite(_connection)
                .AddSemanticDbInterceptors(sp));

        _provider = services.BuildServiceProvider();

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        // Start validation + initialization hosted services; skip BackgroundService (outbox loop).
        foreach (var svc in _provider.GetServices<IHostedService>().Where(s => s is not BackgroundService))
            await svc.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    protected ISemanticDbProcessor Processor =>
        Services.GetRequiredService<ISemanticDbProcessor>();

    protected ISemanticSearcher<ProductChunk> Searcher =>
        Services.CreateScope().ServiceProvider.GetRequiredService<ISemanticSearcher<ProductChunk>>();

    protected TestDbContext CreateDbContext() => _provider
        .CreateAsyncScope()
        .ServiceProvider
        .GetRequiredService<TestDbContext>();
}
