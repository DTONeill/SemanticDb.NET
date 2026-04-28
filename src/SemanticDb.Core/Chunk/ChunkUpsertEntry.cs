namespace SemanticDb.Core.Chunk;

/// <summary>
/// Carries the data needed to insert or update a single chunk in the vector store.
/// </summary>
public record ChunkUpsertEntry(string ChunkName, string EntityId, string? ScopeKey, string PromptContext, ReadOnlyMemory<float> Embedding);
