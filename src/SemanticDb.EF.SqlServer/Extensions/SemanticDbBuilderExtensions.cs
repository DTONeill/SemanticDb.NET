using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SemanticDb.Core.Configuration;
using SemanticDb.EF.Extensions;
using Microsoft.Extensions.DependencyInjection;
using SemanticDb.EF.SqlServer.Hosting;
using SemanticDb.EF.SqlServer.Search;
using SemanticDb.EF.SqlServer.TypeMapping;

namespace SemanticDb.EF.SqlServer.Extensions;

/// <summary>
/// Extension methods for registering the SQL Server semantic search provider.
/// </summary>
public static class SemanticDbBuilderExtensions
{
    /// <summary>
    /// Registers the SQL Server vector search provider for SemanticDb.
    /// Includes all EF Core support — do not call <c>UseEfCore()</c> separately.
    /// </summary>
    public static SemanticDbBuilder UseSqlServer<TContext>(this SemanticDbBuilder builder,
        int vectorDimensions = 1536)
        where TContext : DbContext
    {
        if (builder.ProviderKey is not null)
            throw new InvalidOperationException(
                "A provider is already registered. Do not call UseEfCore() before UseSqlServer(). " +
                "UseSqlServer() already includes EF Core support.");

        builder.Options.VectorDimensions = vectorDimensions;
        builder.UseEfCore<TContext>();
        builder.ProviderKey = "SqlServer";
        builder.Services.AddHostedService<SqlServerVectorValidationService>();
        builder.Services.AddHostedService<SqlServerVectorFeatureConfigurator>();
        builder.Services.AddScoped<SqlServerVectorSearchStrategy>();
        builder.StrategyRegistry.Register(typeof(ISqlServerVectorSearch), typeof(SqlServerVectorSearchStrategy));

        // Register the type mapping plugin so EF Core generates VECTOR(n) in migrations
        builder.Services.AddSingleton<IRelationalTypeMappingSourcePlugin>(
            new VectorTypeMappingSourcePlugin(vectorDimensions));

        return builder;
    }
}
