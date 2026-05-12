using SemanticDb.Core.Search;

namespace SemanticDb.Core.Configuration;

/// <summary>
/// Maps search concept interfaces (e.g. <see cref="SemanticDb.Core.Abstractions.IVectorSearch"/>)
/// to their registered <see cref="ISearchStrategy"/> implementation types.
/// </summary>
/// <remarks>
/// Provider packages populate this registry during setup (e.g. inside <c>UseEfCore()</c>).
/// At query time, <c>SemanticSearcher</c> resolves the implementation type from this registry
/// and then resolves the instance from the DI container.
/// </remarks>
public sealed class SearchStrategyRegistry
{
    private readonly Dictionary<Type, Type> _strategies = new();

    /// <summary>
    /// Registers <paramref name="implType"/> as the <see cref="ISearchStrategy"/> implementation
    /// for the given <paramref name="conceptType"/>.
    /// </summary>
    /// <param name="conceptType">
    /// The concept interface that callers use to identify the strategy
    /// (e.g. <c>typeof(IVectorSearch)</c>).
    /// </param>
    /// <param name="implType">
    /// The concrete <see cref="ISearchStrategy"/> implementation type registered in the DI container.
    /// </param>
    public void Register(Type conceptType, Type implType) => _strategies[conceptType] = implType;

    /// <summary>
    /// Resolves the implementation type for <paramref name="conceptType"/>.
    /// Throws if no strategy has been registered for that concept.
    /// </summary>
    public Type Resolve(Type conceptType) =>
        _strategies.TryGetValue(conceptType, out Type? implType) ? implType
        : throw new InvalidOperationException(
            $"No search strategy registered for '{conceptType.Name}'. " +
            $"Ensure the provider package is configured (e.g. call UseEfCore() or UseSqlServer() after AddSemanticDb()).");

    /// <summary>Returns the concept types for all registered strategies.</summary>
    public IEnumerable<Type> RegisteredConcepts => _strategies.Keys;
}
