using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MtgWebApiSample.Core.Services;
using MtgWebApiSample.SqlServer.Data;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Configuration;
using SemanticDb.EF.Extensions;

namespace MtgWebApiSample.Tests.Infrastructure;

public sealed class SampleApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // ── Remove SQL Server type mapping plugin (IRelationalTypeMappingSourcePlugin) ──
            var typeMappingDescriptors = services
                .Where(d => d.ServiceType == typeof(IRelationalTypeMappingSourcePlugin))
                .ToList();
            foreach (var d in typeMappingDescriptors)
                services.Remove(d);

            // ── Remove SQL Server + in-memory IVectorSearch registrations, use test implementation ──
            services.RemoveAll<IVectorSearch>();
            services.AddScoped<IVectorSearch, TestVectorSearch>();

            // ── Remove AppDbContext (SQL Server) and DbContext mappings ──
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContext>();

            // ── Register TestAppDbContext with SQLite (same in-memory connection for all scopes) ──
            services.AddDbContext<TestAppDbContext>((sp, options) =>
                options.UseSqlite(_connection)
                       .AddSemanticDbInterceptors(sp));

            // Re-expose as AppDbContext and DbContext so existing code (EfChunkStore<AppDbContext>,
            // CardEndpoints, seed endpoint) can resolve them without knowing the concrete type.
            services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<TestAppDbContext>());
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<TestAppDbContext>());

            // ── Replace embedding generator (both unkeyed and the keyed one used internally) ──
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, FakeEmbeddingGenerator>();

            var fakeGenerator = new FakeEmbeddingGenerator();
            var keyedEmbeddingDescriptors = services
                .Where(d => d.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>)
                    && d.ServiceKey is not null)
                .ToList();
            foreach (var d in keyedEmbeddingDescriptors)
                services.Remove(d);
            services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
                SemanticDbBuilder.EmbeddingGeneratorKey, fakeGenerator);

            // ── Replace chat client ──
            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(new FakeChatClient());

            // ── Remove hosted services that require a live SQL Server ──
            var sqlHostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType?.Name is
                        "SqlServerVectorValidationService" or
                        "SqlServerVectorFeatureConfigurator")
                .ToList();
            foreach (var d in sqlHostedServices)
                services.Remove(d);

            // ── Remove MtgSeedService (calls external MTG API — not wanted in tests) ──
            services.RemoveAll<MtgSeedService>();
            var mtgSeedHosted = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType == typeof(MtgSeedService))
                .ToList();
            foreach (var d in mtgSeedHosted)
                services.Remove(d);
        });
    }
}
