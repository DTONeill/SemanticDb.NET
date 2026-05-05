using ECommerceApiSample.Data;
using ECommerceApiSample.Endpoints;
using ECommerceApiSample.Search;
using ECommerceApiSample.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using SemanticDb.Core.Extensions;
using SemanticDb.EF.Extensions;

var builder = WebApplication.CreateBuilder(args);

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]!;
var openAiEmbeddingModel = builder.Configuration["OpenAI:EmbeddingModel"]!;
var openAiChatModel = builder.Configuration["OpenAI:ChatModel"]!;
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Data Source=ecommerce.db";

var openAiClient = new OpenAIClient(openAiApiKey);

// ─── IChatClient ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton(
    openAiClient.GetChatClient(openAiChatModel).AsIChatClient());

// ─── SemanticDb ──────────────────────────────────────────────────────────────
// AddSemanticDb must precede the DbContext registration so AddSemanticDbInterceptors
// can resolve RagInterceptor from the service provider.
builder.Services
    .AddSemanticDb(
        options => { options.MaxRetries = 5; },
        [typeof(ProductsByCategoryChunk).Assembly])
    .UseEfCore<AppDbContext>()
    .UseEmbeddingsProvider(
        openAiClient.GetEmbeddingClient(openAiEmbeddingModel).AsIEmbeddingGenerator());

// ─── Application DbContext ────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlite(connectionString)
           .AddSemanticDbInterceptors(sp));

builder.Services.AddEndpointsApiExplorer();

// ─── Seed service ─────────────────────────────────────────────────────────────
// Registered after AddSemanticDb so SemanticDbInitializationService (IHostedService)
// starts on an empty DB and records the current version before any data is inserted.
// If CatalogSeedService ran first, the init service would enqueue a full re-index for
// every newly seeded row, doubling the outbox entries on first startup.
builder.Services.AddHostedService<CatalogSeedService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapProductEndpoints();
app.MapReviewEndpoints();
app.MapIndexEndpoints();

app.Run();

public partial class Program { }
