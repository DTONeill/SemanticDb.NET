namespace SemanticDb.Core.Configuration;

/// <summary>
/// Holds all registered <see cref="SearchableEntityRegistration"/> instances discovered at startup.
/// </summary>
public sealed class SearchableEntityRegistry
{
    private readonly Dictionary<string, SearchableEntityRegistration> _registrationsByChunkName = [];
    private readonly Dictionary<Type, List<SearchableEntityRegistration>> _byEntityType = [];
    private readonly Dictionary<Type, SearchableEntityRegistration> _byImplementationType = [];

    internal void Register(SearchableEntityRegistration registration)
    {
        if (!_registrationsByChunkName.TryAdd(registration.ChunkName, registration))
            throw new InvalidOperationException(
                $"A searchable entity for '{registration.ChunkName}' is already registered.");

        _byImplementationType[registration.ImplementationType] = registration;

        if (!_byEntityType.TryGetValue(registration.EntityType, out var list))
        {
            list = [];
            _byEntityType[registration.EntityType] = list;
        }

        list.Add(registration);
    }

    /// <summary>Returns <see langword="true"/> if any chunk definition is registered for the given entity type.</summary>
    public bool IsRegistered(Type entityType) =>
        _byEntityType.ContainsKey(entityType);

    /// <summary>Looks up a registration by chunk name. Returns <see langword="false"/> if not found.</summary>
    public bool TryGetByChunkName(string chunkName, out SearchableEntityRegistration? registration) =>
        _registrationsByChunkName.TryGetValue(chunkName, out registration);

    /// <summary>Returns all chunk definitions registered for the given entity type.</summary>
    public IEnumerable<SearchableEntityRegistration> GetRegistrations(Type entityType) =>
        _byEntityType.TryGetValue(entityType, out var list) ? list : [];

    /// <summary>Looks up a registration by its implementation type. Returns <see langword="false"/> if not found.</summary>
    public bool TryGetByImplementationType(Type implementationType, out SearchableEntityRegistration? registration) =>
        _byImplementationType.TryGetValue(implementationType, out registration);

    /// <summary>Returns all registered chunk definitions.</summary>
    public IEnumerable<SearchableEntityRegistration> GetRegistrations() =>
        _registrationsByChunkName.Values;
}
