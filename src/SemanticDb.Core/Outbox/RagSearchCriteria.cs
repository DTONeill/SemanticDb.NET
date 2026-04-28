namespace SemanticDb.Core.Outbox;

public record RagSearchCriteria(RagOutboxStatus StatusFilter, string instanceId, int Take);
