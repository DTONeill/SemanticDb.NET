namespace SemanticDb.Core.Outbox;

/// <summary>
/// Parameters used to query claimed outbox entries for a specific processor instance.
/// </summary>
public record RagSearchCriteria(RagOutboxStatus StatusFilter, string instanceId, int Take);
