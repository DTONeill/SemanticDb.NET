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

    public bool IsRegistered(Type entityType) =>
        _byEntityType.ContainsKey(entityType);

    public bool TryGetByChunkName(string chunkName, out SearchableEntityRegistration? registration) =>
        _registrationsByChunkName.TryGetValue(chunkName, out registration);

    public IEnumerable<SearchableEntityRegistration> GetRegistrations(Type entityType) =>
        _byEntityType.TryGetValue(entityType, out var list) ? list : [];

    public bool TryGetByImplementationType(Type implementationType, out SearchableEntityRegistration? registration) =>
        _byImplementationType.TryGetValue(implementationType, out registration);

    public IEnumerable<SearchableEntityRegistration> GetRegistrations() =>
        _registrationsByChunkName.Values;
}
