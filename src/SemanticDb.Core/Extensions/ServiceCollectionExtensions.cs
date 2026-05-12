using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Hosting;
using SemanticDb.Core.Search;

namespace SemanticDb.Core.Extensions;

/// <summary>
/// Extension methods for registering semantic search services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the semantic search core services and scans the given assemblies
    /// for <see cref="ISearchableEntity{T}"/> implementations.
    /// </summary>
    public static SemanticDbBuilder AddSemanticDb(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        return AddSemanticDb(services, new SemanticDbOptions(), assemblies);
    }

    /// <summary>
    /// Registers the semantic search core services with custom options and scans the given assemblies
    /// for <see cref="ISearchableEntity{T}"/> implementations.
    /// </summary>
    public static SemanticDbBuilder AddSemanticDb(
        this IServiceCollection services,
        Action<SemanticDbOptions> configure,
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
            throw new ArgumentException(
                "At least one assembly must be provided for scanning ISearchableEntity<T> implementations.",
                nameof(assemblies));
        var options = new SemanticDbOptions();
        configure(options);
        return AddSemanticDb(services, options, assemblies);
    }

    private static SemanticDbBuilder AddSemanticDb(
        IServiceCollection services,
        SemanticDbOptions options,
        Assembly[] assemblies)
    {
        var registry = new SearchableEntityRegistry();
        var strategyRegistry = new SearchStrategyRegistry();

        foreach (Assembly assembly in assemblies)
            ScanAssembly(assembly, registry, services);

        services.AddSingleton(options);
        services.AddSingleton(registry);
        services.AddSingleton(strategyRegistry);
        services.AddHostedService<SemanticDbValidationService>();
        services.AddHostedService<SemanticDbInitializationService>();

        var builder = new SemanticDbBuilder(services, options, registry, strategyRegistry);
        services.AddSingleton(builder);

        // Fallback: resolve the unkeyed IEmbeddingGenerator when UseEmbeddingsProvider is not called.
        // UseEmbeddingsProvider overrides this by registering last (last-wins in .NET DI).
        services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
            SemanticDbBuilder.EmbeddingGeneratorKey,
            (sp, _) => sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());

        return builder;
    }

    private static void ScanAssembly(Assembly assembly, SearchableEntityRegistry registry, IServiceCollection services)
    {
        Type searchableType = typeof(ISearchableEntity<,>);

        var implementations = assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == searchableType)
                .Select(i => (ImplementationType: t, EntityType: i.GetGenericArguments()[0],
                    ScopeKeyType: i.GetGenericArguments()[1])));

        foreach (var (implType, entityType, scopeKeyType) in implementations)
        {
            object instance;
            try
            {
                instance = Activator.CreateInstance(implType)
                           ?? throw new InvalidOperationException("Activator returned null.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to instantiate '{implType.FullName}'. " +
                    $"Ensure it has a public parameterless constructor.", ex);
            }

            Type iface = typeof(ISearchableEntity<,>).MakeGenericType(entityType, scopeKeyType);

            MethodInfo isDeleted = iface.GetMethod(nameof(ISearchableEntity<object, bool>.IsDeleted))
                                   ?? throw new InvalidOperationException(
                                       $"'{implType.FullName}' does not implement {nameof(ISearchableEntity<object, object>.ToSearchContent)}.");

            MethodInfo toSearchContent = iface.GetMethod(nameof(ISearchableEntity<object, object>.ToSearchContent))
                                         ?? throw new InvalidOperationException(
                                             $"'{implType.FullName}' does not implement {nameof(ISearchableEntity<object, object>.ToSearchContent)}.");

            MethodInfo toPromptContext = iface.GetMethod(nameof(ISearchableEntity<object, object>.ToPromptContext))
                                         ?? throw new InvalidOperationException(
                                             $"'{implType.FullName}' does not implement {nameof(ISearchableEntity<object, object>.ToPromptContext)}.");

            MethodInfo getScopeKey = iface.GetMethod(nameof(ISearchableEntity<object, object>.GetScopeKey))
                                     ?? throw new InvalidOperationException(
                                         $"'{implType.FullName}' does not implement {nameof(ISearchableEntity<object, object>.GetScopeKey)}.");

            PropertyInfo versionProp = iface.GetProperty(nameof(ISearchableEntity<object, object>.Version))
                                       ?? throw new InvalidOperationException(
                                           $"'{implType.FullName}' does not expose the {nameof(ISearchableEntity<object, object>.Version)} property.");

            registry.Register(new SearchableEntityRegistration
            {
                ChunkName = implType.Name,
                ImplementationType = implType,
                EntityType = entityType,
                Version = (int)versionProp.GetValue(instance)!,
                ToSearchContent = entity => (string)toSearchContent.Invoke(instance, [entity])!,
                ToPromptContext = entity => (string)toPromptContext.Invoke(instance, [entity])!,
                GetScopeKey = entity => getScopeKey.Invoke(instance, [entity]),
                IsDeleted = entity => (bool)isDeleted.Invoke(instance, [entity])!
            });

            services.AddScoped(
                typeof(ISemanticSearcher<>).MakeGenericType(implType),
                typeof(SemanticSearcher<>).MakeGenericType(implType));
        }
    }
}
