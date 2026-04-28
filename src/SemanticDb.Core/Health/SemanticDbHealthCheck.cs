using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Outbox;

namespace SemanticDb.Core.Health;

/// <summary>
/// Reports <see cref="HealthStatus.Degraded"/> when permanently-failed outbox entries exist,
/// and <see cref="HealthStatus.Healthy"/> otherwise.
/// Register via <c>services.AddHealthChecks().AddSemanticDb()</c>.
/// </summary>
public sealed class SemanticDbHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="SemanticDbHealthCheck"/>.
    /// </summary>
    public SemanticDbHealthCheck(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();

        int failedCount = await store.CountByStatusAsync(RagOutboxStatus.Failed, cancellationToken);

        if (failedCount == 0)
            return HealthCheckResult.Healthy("Outbox is healthy — no permanently-failed entries.");

        return HealthCheckResult.Degraded(
            $"Outbox has {failedCount} permanently-failed entr{(failedCount == 1 ? "y" : "ies")}. " +
            "Inspect the RagOutbox table for details.");
    }
}
