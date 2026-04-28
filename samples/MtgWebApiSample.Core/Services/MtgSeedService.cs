using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MtgWebApiSample.Core.Models;

namespace MtgWebApiSample.Core.Services;

public sealed class MtgSeedService : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MtgSeedService> _logger;
    private readonly HttpClient _http;

    public MtgSeedService(
        IServiceScopeFactory scopeFactory,
        ILogger<MtgSeedService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _http = httpClientFactory.CreateClient("MtgApi");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        if (await db.Set<Card>().AnyAsync(cancellationToken))
        {
            _logger.LogInformation("MTG cards already seeded, skipping.");
            return;
        }

        _logger.LogInformation("Fetching MTG cards from API...");

        const int pageSize = 100;
        const int maxPages = 50; // first 5 000 cards is enough for a sample
        int total = 0;

        for (int page = 1; page <= maxPages; page++)
        {
            var response = await _http.GetAsync(
                $"/v1/cards?page={page}&pageSize={pageSize}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MTG API returned {Status} on page {Page}.", response.StatusCode, page);
                break;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<MtgApiCardsResponse>(body, JsonOptions);

            if (apiResponse?.Cards is not { Count: > 0 })
                break;

            var cards = apiResponse.Cards
                .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new Card
                {
                    Id         = c.Id!,
                    Name       = c.Name!,
                    ManaCost   = c.ManaCost,
                    Type       = c.Type,
                    Rarity     = c.Rarity,
                    SetCode    = c.SetCode,
                    Text       = c.Text,
                    FlavorText = c.FlavorText
                })
                .ToList();

            if (cards.Count == 0)
                break;

            await db.Set<Card>().AddRangeAsync(cards, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            total += cards.Count;
            _logger.LogInformation("Seeded page {Page} — {Total} cards total.", page, total);
        }

        _logger.LogInformation("Done. {Total} MTG cards seeded.", total);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
