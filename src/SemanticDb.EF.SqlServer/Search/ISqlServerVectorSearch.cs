namespace SemanticDb.EF.SqlServer.Search;

/// <summary>
/// Concept marker interface for the SQL Server native vector search strategy.
/// Use this type with <c>WithStrategy</c> or call <c>UseSqlServerVectorSearch()</c> on the query.
/// Registered automatically by <c>UseSqlServer()</c>.
/// </summary>
public interface ISqlServerVectorSearch { }
