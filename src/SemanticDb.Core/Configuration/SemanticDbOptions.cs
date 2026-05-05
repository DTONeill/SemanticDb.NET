namespace SemanticDb.Core.Configuration;

/// <summary>
/// Holds the semantic search configuration built at startup.
/// </summary>
public sealed class SemanticDbOptions
{
    /// <summary>
    /// Maximum number of retry attempts before an entry is permanently marked Failed.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Default maximum number of results returned by <see cref="SemanticDb.Core.Abstractions.ISemanticDbService.SearchAsync{TSearchableEntity}"/>
    /// when no explicit limit is provided. Default 25.
    /// </summary>
    public int DefaultSearchLimit { get; set; } = 25;

    /// <summary>
    /// The number of dimensions produced by the configured embedding model.
    /// Must match the model — e.g. 1536 for text-embedding-3-small, 3072 for text-embedding-3-large.
    /// Defaults to 1536.
    /// </summary>
    public int VectorDimensions { get; set; } = 1536;

    /// <summary>
    /// Base delay for exponential back-off between retries (doubles each attempt).
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of outbox entries claimed and processed per cycle. Default 100.
    /// </summary>
    public int OutboxBatchSize { get; set; } = 100;

    /// <summary>
    /// How long a permanently-failed entry must sit before it is automatically reset to
    /// <c>Pending</c> with <c>RetryCount = 0</c> for another attempt. Set to <c>null</c> to
    /// disable automatic recovery of failed entries. Default 1 hour.
    /// </summary>
    public TimeSpan? FailedEntryResetPeriod { get; set; } = TimeSpan.FromHours(1);

}
