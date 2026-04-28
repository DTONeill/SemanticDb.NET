using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Models;

namespace SemanticDb.Core.Hosting;

/// <summary>
/// Runs at startup to detect version changes in <see cref="ISearchableEntity{T}"/> implementations
/// and queues a full re-indexing via the outbox when a version mismatch is detected.
/// </summary>
internal sealed class SemanticDbInitializationService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SearchableEntityRegistry _registry;
    private readonly ILogger<SemanticDbInitializationService> _logger;
    private readonly SemanticDbBuilder _builder;

    public SemanticDbInitializationService(
        IServiceScopeFactory scopeFactory,
        SearchableEntityRegistry registry,
        ILogger<SemanticDbInitializationService> logger,
        SemanticDbBuilder builder)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _logger = logger;
        _builder = builder;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var indexStateStore = scope.ServiceProvider.GetRequiredService<IRagIndexStateStore>();
        var ragOutboxStore = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();
        var providerKey = _builder.ProviderKey ?? "Unknown";

        foreach (var registration in _registry.GetRegistrations())
        {
            var compositeVersion = $"{registration.Version}:{providerKey}";
            var state = await indexStateStore.FindAsync(registration.ChunkName, cancellationToken);

            if (state is not null && state.CompositeVersion == compositeVersion)
            {
                _logger.LogInformation(
                    "Re-index skipped for '{ChunkName}', version not modified",
                    registration.ChunkName);
                continue;
            }

            var claimed = await indexStateStore.TryUpdateVersionAsync(
                registration.ChunkName,
                expectedCompositeVersion: state?.CompositeVersion,
                newCompositeVersion: compositeVersion,
                cancellationToken);

            if (!claimed)
            {
                _logger.LogInformation(
                    "Re-index for '{ChunkName}' already claimed by another instance.",
                    registration.ChunkName);
                continue;
            }

            _logger.LogInformation(
                "Version mismatch for chunk '{ChunkName}': stored={StoredVersion}, current={CurrentVersion}. Queuing re-index.",
                registration.ChunkName,
                state?.CompositeVersion,
                registration.Version);

            await ragOutboxStore.EnqueueReindexAsync(
                registration.ChunkName,
                registration.EntityType.FullName!,
                registration.EntityType,
                cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
