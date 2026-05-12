using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;

namespace SemanticDb.Core.Search;


/// <summary>
/// A fluent builder that accumulates search parameters for a single query against
/// <typeparamref name="TSearchableEntity"/> chunks, then executes via
/// <see cref="ToListAsync"/>.
/// </summary>
/// <typeparam name="TSearchableEntity">
/// The <see cref="ISearchableEntity{T,TScopeKey}"/> implementation being searched.
/// </typeparam>
public sealed class SemanticSearchQuery<TSearchableEntity>
    where TSearchableEntity : ISearchableEntity
{
    private readonly Func<SemanticSearchQuery<TSearchableEntity>, CancellationToken, Task<IReadOnlyList<SemanticDbResult>>> _executor;

    internal string QueryText { get; }
    internal string? ScopeKey { get; private set; }
    /// <summary>
    /// The maximum number of results to return, or <see langword="null"/> to use the
    /// configured <see cref="SemanticDb.Core.Configuration.SemanticDbOptions.DefaultSearchLimit"/>.
    /// </summary>
    internal int? TopK { get; private set; }
    /// <summary>
    /// The concept interface type identifying the selected search strategy.
    /// Defaults to <see cref="IInMemoryVectorSearch"/> (in-memory cosine similarity search).
    /// </summary>
    internal Type StrategyType { get; private set; } = typeof(IInMemoryVectorSearch);

    internal SemanticSearchQuery(
        string text,
        Func<SemanticSearchQuery<TSearchableEntity>, CancellationToken, Task<IReadOnlyList<SemanticDbResult>>> executor)
    {
        QueryText = text;
        _executor = executor;
    }

    /// <summary>
    /// Selects the search strategy to use by its concept interface type.
    /// The concept type must be registered in the <see cref="SemanticDb.Core.Configuration.SearchStrategyRegistry"/>
    /// by the provider package (e.g. <c>UseEfCore()</c> registers <see cref="IInMemoryVectorSearch"/>).
    /// </summary>
    /// <remarks>
    /// Use this method to define extension methods for custom strategies:
    /// <code>
    /// public static SemanticSearchQuery&lt;T&gt; UseMyStrategy&lt;T&gt;(this SemanticSearchQuery&lt;T&gt; query)
    ///     where T : ISearchableEntity =&gt; query.WithStrategy(typeof(IMySearchConcept));
    /// </code>
    /// </remarks>
    public SemanticSearchQuery<TSearchableEntity> WithStrategy(Type conceptType)
    {
        StrategyType = conceptType;
        return this;
    }

    /// <summary>
    /// Selects in-memory cosine similarity search using the registered <see cref="IInMemoryVectorSearch"/> provider.
    /// This is the default strategy when <see cref="WithStrategy"/> is not called.
    /// </summary>
    public SemanticSearchQuery<TSearchableEntity> UseInMemorySearch()
        => WithStrategy(typeof(IInMemoryVectorSearch));

    /// <summary>
    /// Restricts results to chunks whose scope key matches <paramref name="scopeKey"/>.
    /// Omit to search across all scopes.
    /// </summary>
    /// <param name="scopeKey">The scope key value; serialized via <see cref="object.ToString"/>.</param>
    public SemanticSearchQuery<TSearchableEntity> WithScope(object? scopeKey)
    {
        ScopeKey = scopeKey?.ToString();
        return this;
    }

    /// <summary>
    /// Sets the maximum number of results to return.
    /// Defaults to <see cref="SemanticDb.Core.Configuration.SemanticDbOptions.DefaultSearchLimit"/> when omitted.
    /// </summary>
    /// <param name="topK">Must be greater than zero.</param>
    public SemanticSearchQuery<TSearchableEntity> Limit(int topK)
    {
        if (topK <= 0)
            throw new ArgumentOutOfRangeException(nameof(topK), "Limit must be greater than zero.");

        TopK = topK;
        return this;
    }

    /// <summary>
    /// Executes the query and returns the matching results ordered by relevance.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task<IReadOnlyList<SemanticDbResult>> ToListAsync(CancellationToken cancellationToken = default)
        => _executor(this, cancellationToken);
}
