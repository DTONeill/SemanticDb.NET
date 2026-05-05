using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Models;

namespace SemanticDb.Core.Outbox;

internal sealed class RagOutboxProcessor : BackgroundService, ISemanticDbProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemanticDbOptions _options;
    private readonly ILogger<RagOutboxProcessor> _logger;
    private readonly SearchableEntityRegistry _registry;

    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BusyInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(5);
    private readonly string _instanceId = $"{Environment.MachineName}-{Environment.ProcessId}";

    public RagOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        SemanticDbOptions options,
        ILogger<RagOutboxProcessor> logger,
        SearchableEntityRegistry registry)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _registry = registry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await ProcessPendingEntriesAsync(stoppingToken);
            var delay = processed > 0 ? BusyInterval : IdleInterval;
            await Task.Delay(delay, stoppingToken);
        }
    }

    /// <inheritdoc />
    public Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
        => ProcessPendingEntriesAsync(cancellationToken);

    internal async Task<int> ProcessPendingEntriesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var chunkStore = scope.ServiceProvider.GetRequiredService<IChunkStore>();
        var ragOutboxStore = scope.ServiceProvider.GetRequiredService<IRagOutboxStore>();
        var chunkedTableStore = scope.ServiceProvider.GetRequiredService<ISearchableTableStore>();
        var embeddingService =
            scope.ServiceProvider.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>(
                SemanticDbBuilder.EmbeddingGeneratorKey);

        var toProcess = await FetchBatchAsync(ragOutboxStore, cancellationToken);
        if (toProcess.Count == 0)
            return 0;

        var deleteEntries = toProcess.Where(e => e.Status == RagOutboxStatus.ProcessingDelete).ToList();
        var (upsert, softDeletes, loadFailed) =
            await LoadEntitiesAsync(chunkedTableStore, toProcess, cancellationToken);
        var allDeleteEntries = deleteEntries.Concat(softDeletes).ToList();

        var embeddingResults = await GenerateEmbeddingsAsync(embeddingService, upsert, cancellationToken);

        return await ApplyChangesAsync(ragOutboxStore, chunkStore, embeddingResults, allDeleteEntries, loadFailed,
            cancellationToken);
    }

    // ── Phase 0: Fetch ────────────────────────────────────────────────────────

    private async Task<List<RagOutboxEntry>> FetchBatchAsync(
        IRagOutboxStore ragOutboxStore,
        CancellationToken cancellationToken)
    {
        // Reset stale claims from crashed instances
        await ragOutboxStore.SetStaleEntriesToPending(StaleClaimTimeout, cancellationToken);

        // Re-enqueue permanently-failed entries that have been sitting long enough for another attempt
        if (_options.FailedEntryResetPeriod.HasValue)
            await ragOutboxStore.ResetFailedEntriesAsync(_options.FailedEntryResetPeriod.Value, cancellationToken);

        // Atomically claim a batch
        await ragOutboxStore.ClaimBatchAsync(_instanceId, _options.OutboxBatchSize, cancellationToken);

        // Fetch only what this instance claimed
        return await ragOutboxStore
            .ListAsync(new RagSearchCriteria(
                    RagOutboxStatus.Processing,
                    _instanceId,
                    _options.OutboxBatchSize),
                cancellationToken);
    }

    // ── Phase 1: Load entities ────────────────────────────────────────────────

    private async Task<(List<UpsertWorkItem> Upserts, List<RagOutboxEntry> SoftDeletes, List<RagOutboxEntry> Failed)>
        LoadEntitiesAsync(
            ISearchableTableStore searchableTableStore,
            List<RagOutboxEntry> entries,
            CancellationToken cancellationToken)
    {
        var upserts = new List<UpsertWorkItem>();
        var softDeletes = new List<RagOutboxEntry>();
        var failed = new List<RagOutboxEntry>();

        foreach (var group in entries
                     .Where(e => e.Status != RagOutboxStatus.ProcessingDelete)
                     .GroupBy(e => e.ChunkName))
        {
            if (!_registry.TryGetByChunkName(group.Key, out var registration))
            {
                _logger.LogWarning("No chunk definition found for '{ChunkName}'.", group.Key);
                var missingDef = new InvalidOperationException(
                    $"No chunk definition registered for '{group.Key}'.");
                foreach (var entry in group)
                {
                    MarkFailed(entry, missingDef);
                    failed.Add(entry);
                }

                continue;
            }

            IReadOnlyDictionary<string, object?> loaded;
            try
            {
                loaded = await searchableTableStore.LoadEntitiesBatchAsync(
                    registration!.EntityType, group.Select(e => e.EntityId).ToList(), cancellationToken);
            }
            catch (Exception ex)
            {
                foreach (var entry in group)
                {
                    MarkFailed(entry, ex);
                    failed.Add(entry);
                }

                continue;
            }

            foreach (var entry in group)
            {
                if (!loaded.TryGetValue(entry.EntityId, out var entity) || entity is null)
                {
                    _logger.LogWarning("Entity '{EntityId}' of type '{EntityType}' not found.",
                        entry.EntityId, entry.EntityType);
                    var notFound = new InvalidOperationException(
                        $"Entity '{entry.EntityId}' of type '{entry.EntityType}' not found.");
                    MarkFailed(entry, notFound);
                    failed.Add(entry);
                    continue;
                }

                if (registration.IsDeleted(entity))
                {
                    softDeletes.Add(entry);
                    continue;
                }

                upserts.Add(new UpsertWorkItem(
                    entry,
                    registration.ToSearchContent(entity),
                    registration.GetScopeKey(entity)?.ToString(),
                    registration.ToPromptContext(entity)));
            }
        }

        return (upserts, softDeletes, failed);
    }

    // ── Phase 2: Generate embeddings (batched) ───────────────────────────────

    private static async Task<EmbeddingResult[]> GenerateEmbeddingsAsync(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        List<UpsertWorkItem> work,
        CancellationToken cancellationToken)
    {
        if (work.Count == 0)
            return [];

        var contents = work.Select(w => w.Content).ToList();
        try
        {
            var embeddings = await embeddingGenerator.GenerateAsync(contents, null, cancellationToken);
            return work.Select((item, i) => new EmbeddingResult(item, embeddings[i].Vector, null)).ToArray();
        }
        catch (Exception ex)
        {
            return work.Select(item => new EmbeddingResult(item, default, ex)).ToArray();
        }
    }

    // ── Phase 3: Apply changes ────────────────────────────────────────────────

    private async Task<int> ApplyChangesAsync(
        IRagOutboxStore ragOutboxStore,
        IChunkStore chunkStore,
        EmbeddingResult[] embeddingResults,
        List<RagOutboxEntry> deleteEntries,
        List<RagOutboxEntry> loadFailed,
        CancellationToken cancellationToken)
    {
        var successful = embeddingResults.Where(r => r.Error is null).ToList();
        var failed = embeddingResults.Where(r => r.Error is not null).ToList();

        foreach (var r in failed)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                       { ["EntryId"] = r.Item.Entry.Id, ["ChunkName"] = r.Item.Entry.ChunkName }))
                _logger.LogError(r.Error, "Failed to generate embedding for entry '{EntryId}'.", r.Item.Entry.Id);
        }

        if (successful.Count > 0)
        {
            await chunkStore.UpsertBatchAsync(
                successful.Select(r => new ChunkUpsertEntry(
                    r.Item.Entry.ChunkName,
                    r.Item.Entry.EntityId,
                    r.Item.ScopeKey,
                    r.Item.PromptContext,
                    r.Embedding)).ToList(),
                cancellationToken);

            await ragOutboxStore.DeleteBatchAsync(
                successful.Select(r => r.Item.Entry.Id).ToList(),
                cancellationToken);

            _logger.LogInformation(
                "Processed {SuccessCount} outbox entr{Suffix} ({DeleteCount} deleted, {FailCount} failed).",
                successful.Count + deleteEntries.Count,
                successful.Count + deleteEntries.Count == 1 ? "y" : "ies",
                deleteEntries.Count,
                failed.Count + loadFailed.Count);
        }

        var allFailed = failed.Select(r => r.Item.Entry).Concat(loadFailed).ToList();
        if (allFailed.Count > 0)
        {
            foreach (var r in failed)
                MarkFailed(r.Item.Entry, r.Error!);

            await ragOutboxStore.UpsertBatchAsync(allFailed, _instanceId, cancellationToken);
        }

        if (deleteEntries.Count > 0)
        {
            await chunkStore.DeleteBatchAsync(
                deleteEntries.Select(e => new ChunkEntryDeleteCriteria(e.ChunkName, e.EntityId)).ToList(),
                cancellationToken);

            await ragOutboxStore.DeleteBatchAsync(
                deleteEntries.Select(e => e.Id).ToList(),
                cancellationToken);
        }

        return successful.Count + deleteEntries.Count;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void MarkFailed(RagOutboxEntry entry, Exception ex)
    {
        entry.Error = ex.Message;
        entry.ProcessedAt = DateTime.UtcNow;
        entry.ClaimedBy = null;
        entry.ClaimedAt = null;

        if (entry.RetryCount < _options.MaxRetries)
        {
            var delay = _options.RetryBaseDelay * Math.Pow(2, entry.RetryCount);
            entry.RetryCount++;
            entry.Status = RagOutboxStatus.Pending;
            entry.NextRetryAt = DateTime.UtcNow + delay;
        }
        else
        {
            entry.Status = RagOutboxStatus.Failed;
            entry.NextRetryAt = null;
        }
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed record UpsertWorkItem(RagOutboxEntry Entry, string Content, string? ScopeKey, string PromptContext);

    private sealed record EmbeddingResult(UpsertWorkItem Item, ReadOnlyMemory<float> Embedding, Exception? Error);
}
