using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using MtgWebApiSample.Core;
using MtgWebApiSample.Core.Models;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Search;
using SemanticDb.EF.SqlServer.Extensions;

namespace MtgWebApiSample.SqlServer;

public static class SqlServerCardEndpoints
{
    public static IEndpointRouteBuilder MapSqlServerCardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/cards/search/sqlserver", async (
            string q,
            string? manaCost,
            ISemanticSearcher<CardsByManaCostSearchableEntity> searcher,
            DbContext db) =>
        {
            var results = await searcher
                .Query(q)
                .WithScope(manaCost)
                .UseSqlServerVectorSearch()
                .ToListAsync();

            if (results.Count == 0)
                return Results.NotFound();

            var entityIds = results.Select(r => r.EntityId).ToList();

            var cards = await db
                .Set<Card>()
                .Where(c => entityIds.Contains(c.Id))
                .ToListAsync();

            var ordered = entityIds
                .Select(id => cards.FirstOrDefault(c => c.Id == id))
                .Where(c => c is not null)
                .ToList();

            return Results.Ok(ordered);
        });

        app.MapGet("/cards/ask/sqlserver", async (
            string q,
            string? manaCost,
            ISemanticSearcher<CardsByManaCostSearchableEntity> searcher,
            IChatClient chatClient) =>
        {
            var results = await searcher
                .Query(q)
                .WithScope(manaCost)
                .UseSqlServerVectorSearch()
                .ToListAsync();

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

        return app;
    }
}
