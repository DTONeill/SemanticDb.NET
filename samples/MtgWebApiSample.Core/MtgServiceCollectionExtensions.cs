using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MtgWebApiSample.Core.Models;
using MtgWebApiSample.Core.Services;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Extensions;

namespace MtgWebApiSample.Core;

public static class MtgServiceCollectionExtensions
{
    public static SemanticDbBuilder AddMtgCoreServices(
        this IServiceCollection services,
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        services.AddSingleton(chatClient);

        services.AddHttpClient("MtgApi", client =>
        {
            client.BaseAddress = new Uri("https://api.magicthegathering.io");
            client.DefaultRequestHeaders.Add("User-Agent", "MtgWebApiSample/1.0");
        });

        var builder = services
            .AddSemanticDb(
                options => { options.MaxRetries = 5; },
                [typeof(CardsByManaCostSearchableEntity).Assembly])
            .UseEmbeddingsProvider(embeddingGenerator);

        // Registered after AddSemanticDb so SemanticDbInitializationService starts
        // on an empty DB (recording the version) before MtgSeedService populates it.
        // If the order were reversed the init service would enqueue a full re-index for every
        // card that was just seeded, doubling the outbox entries on first startup.
        services.AddHostedService<MtgSeedService>();

        return builder;
    }
}
