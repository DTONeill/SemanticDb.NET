namespace SemanticDb.Core.Outbox;

public enum RagOutboxStatus
{
    Pending,
    PendingDelete,
    Failed,
    Processing,
    ProcessingDelete
}
