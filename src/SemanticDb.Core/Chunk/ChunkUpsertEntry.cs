namespace SemanticDb.Core.Chunk;

public record ChunkUpsertEntry(string ChunkName, string EntityId, string? ScopeKey, string PromptContext, ReadOnlyMemory<float> Embedding);
