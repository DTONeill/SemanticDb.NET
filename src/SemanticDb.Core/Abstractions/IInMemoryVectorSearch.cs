namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Concept marker interface for the in-memory cosine-similarity search strategy.
/// Use this type with <c>WithStrategy</c> or call <c>UseInMemorySearch()</c> on the query.
/// Registered automatically by <c>UseEfCore()</c>.
/// </summary>
public interface IInMemoryVectorSearch { }
