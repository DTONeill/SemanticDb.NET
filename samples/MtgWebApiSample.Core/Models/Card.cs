namespace MtgWebApiSample.Core.Models;

public class Card
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? ManaCost { get; set; }
    public string? Type { get; set; }
    public string? Rarity { get; set; }
    public string? SetCode { get; set; }
    public string? Text { get; set; }
    public string? FlavorText { get; set; }
}
