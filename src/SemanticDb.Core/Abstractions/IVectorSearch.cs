using System.ComponentModel;
using SemanticDb.Core.Chunk;

namespace SemanticDb.Core.Abstractions;

/// <summary>
/// Defines the contract for performing vector similarity searches.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IVectorSearch
{
    /// <summary>
    /// Searches for chunks similar to the given query vector.
    /// </summary>
    /// <param name="chunkName">The chunk definition to search against.</param>
    /// <param name="queryVector">The embedding vector of the query.</param>
    /// <param name="scopeKey">An optional scope key to restrict results.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<IReadOnlyList<SemanticDbResult>> SearchAsync(
        string chunkName,
        float[] queryVector,
        string? scopeKey,
        int limit,
        CancellationToken cancellationToken);
}
