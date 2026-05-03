using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SemanticDb.EF.SqlServer.Hosting;

/// <summary>
/// Validates that the SQL Server instance supports native vector search (CU17+).
/// </summary>
internal sealed class SqlServerVectorValidationService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SqlServerVectorValidationService> _logger;

    public SqlServerVectorValidationService(
        IServiceScopeFactory scopeFactory,
        ILogger<SqlServerVectorValidationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

        await using var connection = new SqlConnection(dbContext.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(128))";
        string? version = (string?)await command.ExecuteScalarAsync(cancellationToken);

        if (version is null)
            throw new InvalidOperationException("Could not determine SQL Server version.");

        string[] parts = version.Split('.');
        if (parts.Length == 0 || !int.TryParse(parts[0], out int major))
            throw new InvalidOperationException(
                $"Could not parse SQL Server version string: '{version}'. " +
                "Expected format: '<major>.<minor>.<build>.<revision>'.");

        // SQL Server 2025 = major 17
        if (major < 17)
            throw new InvalidOperationException(
                $"SQL Server native vector search requires SQL Server 2025+. " +
                $"Detected version: {version}. " +
                $"Upgrade your SQL Server instance or use the in-memory fallback via UseEfCore<TContext>().");

        _logger.LogInformation("SQL Server vector search validated. Version: {Version}", version);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
