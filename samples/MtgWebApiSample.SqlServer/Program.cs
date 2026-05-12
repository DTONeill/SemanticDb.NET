using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using MtgWebApiSample.Core;
using MtgWebApiSample.Core.Models;
using MtgWebApiSample.SqlServer;
using MtgWebApiSample.SqlServer.Data;
using SemanticDb.Core.Abstractions;
using SemanticDb.EF.Extensions;
using SemanticDb.EF.SqlServer.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]!;
var openAiEmbeddingModel = builder.Configuration["OpenAI:EmbeddingModel"]!;
var openAiChatModel = builder.Configuration["OpenAI:ChatModel"]!;

var openAiClient = new OpenAIClient(openAiApiKey);

// ─── Core services + SemanticDb ─────────────────────────────────────────
builder.Services
    .AddMtgCoreServices(
        openAiClient.GetChatClient(openAiChatModel).AsIChatClient(),
        openAiClient.GetEmbeddingClient(openAiEmbeddingModel).AsIEmbeddingGenerator())
    .UseSqlServer<AppDbContext>();

// ─── Application DbContext ───────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>((sp, options) => options
    .UseSqlServer(connectionString)
    .AddSemanticDbInterceptors(sp));

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapCardEndpoints();
app.MapSqlServerCardEndpoints();

if (app.Environment.IsEnvironment("Test"))
{
    app.MapPost("/test/seed", async (
        List<Card> cards,
        AppDbContext db,
        ISemanticDbProcessor processor,
        CancellationToken ct) =>
    {
        db.Cards.AddRange(cards);
        await db.SaveChangesAsync(ct);
        await processor.ProcessPendingAsync(ct);
        return Results.Ok(new { seeded = cards.Count });
    });
}

app.Run();

public partial class Program { }
