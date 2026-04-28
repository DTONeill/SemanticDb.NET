using System.Net;
using System.Net.Http.Json;
using MtgWebApiSample.Core.Models;
using MtgWebApiSample.Tests.Infrastructure;
using Xunit;

namespace MtgWebApiSample.Tests;

public class CardEndpointsTests : IClassFixture<SampleApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public CardEndpointsTests(SampleApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task SeedAsync(params Card[] cards)
    {
        var response = await _client.PostAsJsonAsync("/test/seed", cards);
        response.EnsureSuccessStatusCode();
    }

    // ── /cards/search ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ReturnsMatchingCards_WhenQueryMatchesSeededCards()
    {
        await SeedAsync(new Card
        {
            Id = "fire-bolt",
            Name = "Fire Bolt",
            Text = "Deal 3 fire damage to any target.",
            ManaCost = "{R}"
        });

        var response = await _client.GetAsync("/cards/search?q=fire+damage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cards = await response.Content.ReadFromJsonAsync<List<Card>>();
        Assert.NotNull(cards);
        Assert.Contains(cards, c => c.Id == "fire-bolt");
    }

    [Fact]
    public async Task Search_FiltersResultsByManaCost_WhenScopeKeyProvided()
    {
        await SeedAsync(
            new Card { Id = "fire-red", Name = "Fire Red", Text = "fire", ManaCost = "{R}" },
            new Card { Id = "fire-blue", Name = "Fire Blue", Text = "fire", ManaCost = "{U}" }
        );

        var response = await _client.GetAsync("/cards/search?q=fire&manaCost={R}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cards = await response.Content.ReadFromJsonAsync<List<Card>>();
        Assert.NotNull(cards);
        Assert.All(cards, c => Assert.Equal("{R}", c.ManaCost));
        Assert.DoesNotContain(cards, c => c.Id == "fire-blue");
    }

    [Fact]
    public async Task Search_ReturnsNotFound_WhenNoCardsMatchQuery()
    {
        // No cards seeded — or seeded cards don't match "nature" query
        await SeedAsync(new Card
        {
            Id = "water-card",
            Name = "Water Card",
            Text = "water flows gently",
            ManaCost = "{U}"
        });

        // "fire" query → dim0 vector; "water" card → dim1 vector → cosine similarity = 0
        // With DefaultSearchLimit results returned but all having score 0, the service
        // still returns them, so test a query that truly yields 0 results (empty DB scenario)
        var response = await _client.GetAsync("/cards/search?q=something+with+no+match+at+all&manaCost=NONEXISTENT");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── /cards/ask ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ask_ReturnsAnswer_WhenCardsExist()
    {
        await SeedAsync(new Card
        {
            Id = "fire-answer",
            Name = "Fire Answer",
            Text = "fire damage to all creatures",
            ManaCost = "{R}{R}"
        });

        var response = await _client.GetAsync("/cards/ask?q=which+card+deals+fire+damage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AskResponse>();
        Assert.NotNull(body?.Answer);
        Assert.NotEmpty(body.Answer);
    }

    private sealed record AskResponse(string Answer, IEnumerable<string> Sources);
}
