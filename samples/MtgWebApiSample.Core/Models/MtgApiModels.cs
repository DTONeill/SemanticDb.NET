using System.Text.Json.Serialization;

namespace MtgWebApiSample.Core.Models;

public sealed record MtgApiCardsResponse(
    [property: JsonPropertyName("cards")] IReadOnlyList<MtgApiCard>? Cards);

public sealed record MtgApiCard(
    [property: JsonPropertyName("id")]       string? Id,
    [property: JsonPropertyName("name")]     string? Name,
    [property: JsonPropertyName("manaCost")] string? ManaCost,
    [property: JsonPropertyName("type")]     string? Type,
    [property: JsonPropertyName("rarity")]   string? Rarity,
    [property: JsonPropertyName("set")]      string? SetCode,
    [property: JsonPropertyName("text")]     string? Text,
    [property: JsonPropertyName("flavor")]   string? FlavorText);
