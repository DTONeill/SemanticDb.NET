using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Models;
using SemanticDb.Core.Outbox;

namespace SemanticDb.Tests.Core.Outbox;

public class RagOutboxProcessorTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly Mock<IChunkStore> _chunkStore = new();
    private readonly Mock<IRagOutboxStore> _outboxStore = new();
    private readonly Mock<ISearchableTableStore> _tableStore = new();
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _embeddingGenerator = new();
    private readonly SearchableEntityRegistry _registry = new();
    private readonly SemanticDbOptions _options = new() { MaxRetries = 3, RetryBaseDelay = TimeSpan.FromSeconds(5) };

    public RagOutboxProcessorTests()
    {
        // Use a real ServiceProvider to support GetRequiredKeyedService used by the processor.
        var services = new ServiceCollection();
        services.AddSingleton(_chunkStore.Object);
        services.AddSingleton(_outboxStore.Object);
        services.AddSingleton(_tableStore.Object);
        services.AddKeyedSingleton(SemanticDbBuilder.EmbeddingGeneratorKey, _embeddingGenerator.Object);
        var sp = services.BuildServiceProvider();

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(sp);
        _scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _outboxStore.Setup(s => s.SetStaleEntriesToPending(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _outboxStore.Setup(s => s.ClaimBatchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private RagOutboxProcessor CreateProcessor() =>
        new(_scopeFactory.Object, _options, NullLogger<RagOutboxProcessor>.Instance, _registry);

    private void RegisterChunk(string chunkName, Func<object, object?>? getScopeKey = null, Func<object, bool>? isDeleted = null) =>
        _registry.Register(new SearchableEntityRegistration
        {
            ChunkName = chunkName,
            ImplementationType = typeof(FakeSearchableEntity),
            EntityType = typeof(FakeEntity),
            Version = 1,
            ToSearchContent = _ => "content",
            ToPromptContext = _ => "context",
            GetScopeKey = getScopeKey ?? (_ => null),
            IsDeleted = isDeleted ?? (_ => false),
        });

    [Fact]
    public async Task ProcessPendingEntriesAsync_ReturnsZero_WhenNoPendingEntries()
    {
        _outboxStore.Setup(s => s.ListAsync(It.IsAny<RagSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateProcessor().ProcessPendingEntriesAsync(CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ProcessPendingEntriesAsync_DeletesChunkAndOutboxEntry_ForPendingDelete()
    {
        // After claiming, PendingDelete becomes ProcessingDelete — simulate what ClaimBatchAsync does.
        var entry = new RagOutboxEntry
        {
            EntityType = "T",
            EntityId = "42",
            ChunkName = "MyChunk",
            Status = RagOutboxStatus.ProcessingDelete,
        };
        _outboxStore.Setup(s => s.ListAsync(It.IsAny<RagSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);
        _chunkStore.Setup(s => s.DeleteBatchAsync(It.IsAny<IReadOnlyList<ChunkEntryDeleteCriteria>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _outboxStore.Setup(s => s.DeleteBatchAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateProcessor().ProcessPendingEntriesAsync(CancellationToken.None);

        Assert.Equal(1, result);
        _chunkStore.Verify(s => s.DeleteBatchAsync(
            It.Is<IReadOnlyList<ChunkEntryDeleteCriteria>>(l => l.Count == 1 && l[0].ChunkName == "MyChunk" && l[0].EntityId == "42"),
            It.IsAny<CancellationToken>()), Times.Once);
        _outboxStore.Verify(s => s.DeleteBatchAsync(
            It.Is<IReadOnlyList<Guid>>(ids => ids.Single() == entry.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingEntriesAsync_MarksFailed_ForUnknownChunkName()
    {
        var entry = new RagOutboxEntry
        {
            EntityType = "T",
            EntityId = "1",
            ChunkName = "UnknownChunk",
            Status = RagOutboxStatus.Processing,
        };
        _outboxStore.Setup(s => s.ListAsync(It.IsAny<RagSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);

        List<RagOutboxEntry> upserted = [];
        _outboxStore
            .Setup(s => s.UpsertBatchAsync(It.IsAny<IReadOnlyList<RagOutboxEntry>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<RagOutboxEntry>, string, CancellationToken>((e, _, _) => upserted.AddRange(e))
            .Returns(Task.CompletedTask);

        await CreateProcessor().ProcessPendingEntriesAsync(CancellationToken.None);

        Assert.Single(upserted);
        Assert.Contains("UnknownChunk", upserted[0].Error);
    }

    [Fact]
    public async Task ProcessPendingEntriesAsync_UpsertsChunk_OnSuccessfulEmbedding()
    {
        RegisterChunk("MyChunk");
        var entry = new RagOutboxEntry
        {
            EntityType = typeof(FakeEntity).FullName!,
            EntityId = "entity-1",
            ChunkName = "MyChunk",
            Status = RagOutboxStatus.Processing,
        };
        _outboxStore.Setup(s => s.ListAsync(It.IsAny<RagSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);
        _tableStore
            .Setup(s => s.LoadEntitiesBatchAsync(typeof(FakeEntity), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["entity-1"] = new FakeEntity() });
        _embeddingGenerator
            .Setup(s => s.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[] { 0.1f, 0.2f })]));
        _chunkStore.Setup(s => s.UpsertBatchAsync(It.IsAny<IReadOnlyList<ChunkUpsertEntry>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _outboxStore.Setup(s => s.DeleteBatchAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateProcessor().ProcessPendingEntriesAsync(CancellationToken.None);

        Assert.Equal(1, result);
        _chunkStore.Verify(s => s.UpsertBatchAsync(
            It.Is<IReadOnlyList<ChunkUpsertEntry>>(l => l.Count == 1 && l[0].EntityId == "entity-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkFailed_IncrementsRetryCount_WhenBelowMaxRetries()
    {
        RegisterChunk("MyChunk");
        var entry = new RagOutboxEntry
        {
            EntityType = typeof(FakeEntity).FullName!,
            EntityId = "entity-1",
            ChunkName = "MyChunk",
            Status = RagOutboxStatus.Processing,
            RetryCount = 0,
        };
        _outboxStore.Setup(s => s.ListAsync(It.IsAny<RagSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);
        _tableStore
            .Setup(s => s.LoadEntitiesBatchAsync(typeof(FakeEntity), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["entity-1"] = new FakeEntity() });
        _embeddingGenerator
            .Setup(s => s.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("embedding error"));

        List<RagOutboxEntry> upserted = [];
        _outboxStore
            .Setup(s => s.UpsertBatchAsync(It.IsAny<IReadOnlyList<RagOutboxEntry>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<RagOutboxEntry>, string, CancellationToken>((e, _, _) => upserted.AddRange(e))
            .Returns(Task.CompletedTask);

        await CreateProcessor().ProcessPendingEntriesAsync(CancellationToken.None);

        Assert.Single(upserted);
        Assert.Equal(RagOutboxStatus.Pending, upserted[0].Status);
        Assert.Equal(1, upserted[0].RetryCount);
        Assert.NotNull(upserted[0].NextRetryAt);
    }

    [Fact]
    public async Task MarkFailed_SetsStatusToFailed_WhenMaxRetriesExceeded()
    {
        RegisterChunk("MyChunk");
        var entry = new RagOutboxEntry
        {
            EntityType = typeof(FakeEntity).FullName!,
            EntityId = "entity-1",
            ChunkName = "MyChunk",
            Status = RagOutboxStatus.Processing,
            RetryCount = 3,
        };
        _outboxStore.Setup(s => s.ListAsync(It.IsAny<RagSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);
        _tableStore
            .Setup(s => s.LoadEntitiesBatchAsync(typeof(FakeEntity), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["entity-1"] = new FakeEntity() });
        _embeddingGenerator
            .Setup(s => s.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("embedding error"));

        List<RagOutboxEntry> upserted = [];
        _outboxStore
            .Setup(s => s.UpsertBatchAsync(It.IsAny<IReadOnlyList<RagOutboxEntry>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<RagOutboxEntry>, string, CancellationToken>((e, _, _) => upserted.AddRange(e))
            .Returns(Task.CompletedTask);

        await CreateProcessor().ProcessPendingEntriesAsync(CancellationToken.None);

        Assert.Single(upserted);
        Assert.Equal(RagOutboxStatus.Failed, upserted[0].Status);
        Assert.Null(upserted[0].NextRetryAt);
    }

    [Fact]
    public async Task ProcessPendingEntriesAsync_StoresScopeKey_FromGetScopeKey()
    {
        RegisterChunk("MyChunk", getScopeKey: _ => 99);
        var entry = new RagOutboxEntry
        {
            EntityType = typeof(FakeEntity).FullName!,
            EntityId = "entity-1",
            ChunkName = "MyChunk",
            Status = RagOutboxStatus.Processing,
        };
        _outboxStore.Setup(s => s.ListAsync(It.IsAny<RagSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);
        _tableStore
            .Setup(s => s.LoadEntitiesBatchAsync(typeof(FakeEntity), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["entity-1"] = new FakeEntity() });
        _embeddingGenerator
            .Setup(s => s.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[] { 0.1f })]));

        List<ChunkUpsertEntry> upserted = [];
        _chunkStore
            .Setup(s => s.UpsertBatchAsync(It.IsAny<IReadOnlyList<ChunkUpsertEntry>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<ChunkUpsertEntry>, CancellationToken>((entries, _) => upserted.AddRange(entries))
            .Returns(Task.CompletedTask);
        _outboxStore.Setup(s => s.DeleteBatchAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateProcessor().ProcessPendingEntriesAsync(CancellationToken.None);

        Assert.Single(upserted);
        Assert.Equal("99", upserted[0].ScopeKey);
    }

    [Fact]
    public async Task ProcessPendingEntriesAsync_DeletesChunks_WhenLoadedEntityIsMarkedDeleted()
    {
        // Simulates the race: processor claimed a Processing entry, but by load time the entity is soft-deleted.
        RegisterChunk("MyChunk", isDeleted: _ => true);
        var entry = new RagOutboxEntry
        {
            EntityType = typeof(FakeEntity).FullName!,
            EntityId = "entity-1",
            ChunkName = "MyChunk",
            Status = RagOutboxStatus.Processing,
        };
        _outboxStore.Setup(s => s.ListAsync(It.IsAny<RagSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);
        _tableStore
            .Setup(s => s.LoadEntitiesBatchAsync(typeof(FakeEntity), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["entity-1"] = new FakeEntity() });
        _chunkStore.Setup(s => s.DeleteBatchAsync(It.IsAny<IReadOnlyList<ChunkEntryDeleteCriteria>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _outboxStore.Setup(s => s.DeleteBatchAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateProcessor().ProcessPendingEntriesAsync(CancellationToken.None);

        Assert.Equal(1, result);
        _embeddingGenerator.Verify(
            s => s.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _chunkStore.Verify(s => s.DeleteBatchAsync(
            It.Is<IReadOnlyList<ChunkEntryDeleteCriteria>>(l => l.Count == 1 && l[0].EntityId == "entity-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class FakeEntity { }
    private sealed class FakeSearchableEntity : ISearchableEntity { }
}
