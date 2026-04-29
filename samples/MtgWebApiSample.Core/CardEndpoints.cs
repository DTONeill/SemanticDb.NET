using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using MtgWebApiSample.Core.Models;
using SemanticDb.Core.Abstractions;

namespace MtgWebApiSample.Core;

public record UpdateCardRequest(
    string? ManaCost,
    string? Type,
    string? Rarity,
    string? SetCode,
    string? Text,
    string? FlavorText);

public static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/cards/search", async (
            string q,
            string? manaCost,
            ISemanticDbService semanticSearch,
            DbContext db) =>
        {
            var results = manaCost is null
                ? await semanticSearch.SearchAsync<CardsByManaCostSearchableEntity>(query: q)
                : await semanticSearch.SearchAsync<CardsByManaCostSearchableEntity, string>(query: q, manaCost);

            if (results.Count == 0)
                return Results.NotFound();

            var entityIds = results.Select(r => r.EntityId).ToList();

            var cards = await db.Set<Card>()
                .Where(c => entityIds.Contains(c.Id))
                .ToListAsync();

            var ordered = entityIds
                .Select(id => cards.FirstOrDefault(c => c.Id == id))
                .Where(c => c is not null)
                .ToList();

            return Results.Ok(ordered);
        });

        app.MapGet("/cards/ask", async (
            string q,
            string? manaCost,
            ISemanticDbService semanticSearch,
            IChatClient chatClient) =>
        {
            var results = manaCost is null
                ? await semanticSearch.SearchAsync<CardsByManaCostSearchableEntity>(query: q)
                : await semanticSearch.SearchAsync<CardsByManaCostSearchableEntity, string>(query: q, manaCost);

            if (results.Count == 0)
                return Results.NotFound();

            var context = string.Join("\n\n", results.Select(r => r.PromptContext));

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System,
                    "You are a Magic: The Gathering expert. Answer the user's question based solely on the cards below. " +
                    "If the answer cannot be found in the provided cards, say so.\n\n" +
                    $"Cards:\n{context}"),
                new(ChatRole.User, q)
            };

            var response = await chatClient.GetResponseAsync(messages);

            return Results.Ok(new
            {
                answer = response.Text,
                sources = results.Select(r => r.EntityId)
            });
        });

        app.MapPut("/cards/{name}", async (
            string name,
            UpdateCardRequest body,
            DbContext db) =>
        {
            var card = await db.Set<Card>().FirstOrDefaultAsync(c => c.Name == name);
            if (card is null)
                return Results.NotFound();

            card.ManaCost = body.ManaCost;
            card.Type = body.Type;
            card.Rarity = body.Rarity;
            card.SetCode = body.SetCode;
            card.Text = body.Text;
            card.FlavorText = body.FlavorText;

            await db.SaveChangesAsync();
            return Results.Ok(card);
        });

        return app;
    }
}
