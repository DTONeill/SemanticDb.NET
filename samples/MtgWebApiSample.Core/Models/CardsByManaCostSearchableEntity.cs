using SemanticDb.Core.Abstractions;

namespace MtgWebApiSample.Core.Models;

public class CardsByManaCostSearchableEntity : ISearchableEntity<Card, string>
{
    public string ToSearchContent(Card entity) => $"card name: {entity.Name}, " +
                                                  $"manaCost: {entity.ManaCost}, " +
                                                  $"text: {entity.Text}" +
                                                  $"flavor text: {entity.FlavorText}" +
                                                  $"type: {entity.Type}";

    public string ToPromptContext(Card entity) => ToSearchContent(entity);

    public object? GetScopeKey(Card entity) => entity.ManaCost;

    public int Version => 3;
}
