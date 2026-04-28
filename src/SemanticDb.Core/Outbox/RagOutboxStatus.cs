namespace SemanticDb.Core.Outbox;

/// <summary>
/// Represents the lifecycle state of a <see cref="SemanticDb.Core.Models.RagOutboxEntry"/>.
/// </summary>
public enum RagOutboxStatus
{
    /// <summary>Queued for indexing. The entry will be claimed by the next available processor.</summary>
    Pending,

    /// <summary>Queued for deletion. The corresponding chunk will be removed from the vector store.</summary>
    PendingDelete,

    /// <summary>All retry attempts have been exhausted. Manual intervention is required.</summary>
    Failed,

    /// <summary>Claimed by a processor instance and currently being indexed.</summary>
    Processing,

    /// <summary>Claimed by a processor instance and currently being deleted from the vector store.</summary>
    ProcessingDelete
}
