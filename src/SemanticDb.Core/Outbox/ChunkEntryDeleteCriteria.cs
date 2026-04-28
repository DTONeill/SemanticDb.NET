namespace SemanticDb.Core.Outbox;

/// <summary>
/// Identifies a chunk entry to delete by chunk name and entity ID.
/// </summary>
public record ChunkEntryDeleteCriteria(string ChunkName, string EntityId);
