namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Provides manual control over the outbox processing pipeline.
/// Useful in environments without a long-running host (Azure Functions, console apps, tests).
/// </summary>
public interface ISemanticDbProcessor
{
    /// <summary>
    /// Processes all pending outbox entries: generates embeddings and upserts/deletes chunks.
    /// </summary>
    /// <returns>The number of entries successfully processed.</returns>
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
