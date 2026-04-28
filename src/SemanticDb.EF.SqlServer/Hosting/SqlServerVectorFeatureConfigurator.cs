using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SemanticDb.EF.SqlServer.Hosting;

/// <summary>
/// Enables the SQL Server vector search feature flag at startup.
/// Requires sysadmin or serveradmin rights — logs a warning if insufficient permissions.
/// </summary>
internal sealed class SqlServerVectorFeatureConfigurator : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SqlServerVectorFeatureConfigurator> _logger;

    public SqlServerVectorFeatureConfigurator(
        IServiceScopeFactory scopeFactory,
        ILogger<SqlServerVectorFeatureConfigurator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                                  EXEC sp_configure 'show advanced options', 1;
                                  RECONFIGURE;
                                  EXEC sp_configure 'vector search', 1;
                                  RECONFIGURE;
                                  """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("SQL Server vector search feature flag enabled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not enable SQL Server vector search feature flag. " +
                "Run the following manually with sysadmin rights: " +
                "EXEC sp_configure 'vector search', 1; RECONFIGURE;");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
