using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Outbox;
using SemanticDb.Core.Services;
using SemanticDb.EF.Interceptors;
using SemanticDb.EF.Search;
using SemanticDb.EF.Stores;

namespace SemanticDb.EF.Extensions;

/// <summary>
/// Extension methods for registering the EF Core provider on a <see cref="SemanticDbBuilder"/>.
/// </summary>
public static class SemanticDbBuilderExtensions
{
    /// <summary>
    /// Registers the EF Core provider for SemanticDb.
    /// Call <see cref="AddSemanticDbInterceptors"/> inside your
    /// <c>AddDbContext</c> options to attach the change-tracking interceptor.
    /// </summary>
    public static SemanticDbBuilder UseEfCore<TContext>(this SemanticDbBuilder builder)
        where TContext : DbContext
    {
        builder.Services.AddSingleton<RagOutboxProcessor>();
        builder.Services.AddSingleton<ISemanticDbProcessor>(sp => sp.GetRequiredService<RagOutboxProcessor>());
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RagOutboxProcessor>());
        builder.Services.AddSingleton<RagInterceptor>();
        builder.Services.AddScoped<IChunkStore, EfChunkStore<TContext>>();
        builder.Services.AddScoped<ISearchableTableStore, EfSearchableTableStore>();
        builder.Services.AddScoped<IRagOutboxStore, EfRagOutboxStore>();
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());
        builder.Services.AddScoped<IRagIndexStateStore, EfRagIndexStateStore>();
        builder.Services.AddScoped<IVectorSearch, InMemoryVectorSearch>();
        builder.Services.AddScoped<ISemanticDbService, SemanticDbService>();

        builder.ProviderKey = "EF";
        return builder;
    }

    /// <summary>
    /// Adds the SemanticDb interceptor to the DbContext options.
    /// Call this inside <c>AddDbContext</c>:
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;((sp, opt) =>
    ///     opt.UseSqlServer(...)
    ///        .AddSemanticDbInterceptors(sp));
    /// </code>
    /// </summary>
    public static DbContextOptionsBuilder AddSemanticDbInterceptors(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider sp)
    {
        return optionsBuilder.AddInterceptors(sp.GetRequiredService<RagInterceptor>());
    }
}
