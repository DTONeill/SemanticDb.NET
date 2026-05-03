<p align="center">
  <img src="assets/logo.jpg" alt="SemanticDb logo" width="200" />
</p>

# SemanticDb

[![NuGet](https://img.shields.io/nuget/v/SemanticDb.Core.svg)](https://www.nuget.org/packages/SemanticDb.Core)
[![NuGet](https://img.shields.io/nuget/dt/SemanticDb.Core.svg)](https://www.nuget.org/packages/SemanticDb.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## Why

SemanticDb is for .NET developers who already have an EF Core application and want to add semantic/RAG search to their existing entities without building a separate ingestion pipeline. Unlike document-ingestion libraries, there is no file import step -- your existing SaveChanges calls automatically queue indexing work via an EF Core interceptor. You define how each entity is indexed in one class, register the library, and search is available with no changes to your domain models or your existing DbContext workflow.

```csharp
// 1. Define how your entity is indexed
public class PrescriptionSearchable : ISearchableEntity<Prescription, int>
{
    public string ToSearchContent(Prescription entity) =>
        $"Prescription dated {entity.Date}: {entity.Medication} {entity.Dosage}";

    // Optional — defaults to ToSearchContent if omitted
    public string ToPromptContext(Prescription entity) =>
        $"The patient takes {entity.Medication} {entity.Dosage} since {entity.Date}.";

    public object? GetScopeKey(Prescription entity) => entity.PatientId;
}

// 2. Register in Program.cs
services.AddSemanticDb(typeof(PrescriptionSearchable).Assembly)
    .UseSqlServer<AppDbContext>();

// 3. Apply schema in your DbContext
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);
    builder.ApplySemanticDbConfiguration();
    builder.UseSqlServerVectorSearch(); // configures VECTOR(1536) column
}

// 4. Attach the interceptor
services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(connectionString)
           .AddSemanticDbInterceptors(sp));

// 5. Search — results include PromptContext, ready to pass to an LLM
var results = await semanticSearchService.SearchAsync<PrescriptionSearchable, int>(
    query: "blood pressure medication",
    scopeKey: patientId);

foreach (var result in results)
{
    Console.WriteLine($"[{result.Score:P0}] {result.PromptContext}");
}
```

---

## Table of Contents

- [Features](#features)
- [Packages](#packages)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Defining Searchable Entities](#defining-searchable-entities)
- [Configuration](#configuration)
- [Versioning and Re-indexing](#versioning-and-re-indexing)
  - [Automatic re-indexing on version change](#automatic-re-indexing-on-version-change)
  - [Manual re-indexing with ISemanticDbIndexer](#manual-re-indexing-with-isemanticdbindexer)
- [Using Search Results with an LLM](#using-search-results-with-an-llm)
- [Using an Alternative Provider](#using-an-alternative-provider-without-sql-server)
- [Architecture](#architecture)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)

---

## Features

- **Zero domain pollution** — your entity classes remain unchanged; indexing logic lives in dedicated classes
- **Automatic change tracking** — EF Core interceptor detects `INSERT`, `UPDATE`, and `DELETE` and queues indexing work automatically
- **Resilient async pipeline** — outbox pattern ensures embeddings are generated and stored reliably, even across restarts
- **Automatic re-indexing** — increment `Version` on your searchable class to trigger a full re-index transparently at startup; use `ISemanticDbIndexer` to trigger reindexing manually for bulk operations or external changes
- **Scoped search** — partition your index by tenant, patient, or any key using any type; no manual `.ToString()` required
- **Prompt context on results** — `SemanticDbResult.PromptContext` is stored alongside the embedding, eliminating an extra DB round-trip before feeding results to an LLM
- **Type-safe search API** — `SearchAsync<TSearchableEntity, TScopeKey>()` enforces the correct scope key type at compile time; passing the wrong type is a build error
- **Horizontal scaling safe** — optimistic concurrency prevents duplicate re-indexing across multiple instances
- **Native SQL Server vector search** — uses `VECTOR_DISTANCE` for fast similarity search directly in SQL Server 2025
- **Pluggable vector providers** — swap the vector store without changing your application code

---

## Packages

| Package | Description |
|---|---|
| `SemanticDb.Core` | Core abstractions, interfaces, and outbox processor |
| `SemanticDb.EF` | EF Core provider: interceptor, outbox store, chunk store, in-memory vector search fallback |
| `SemanticDb.EF.SqlServer` | SQL Server 2025 native vector search via `VECTOR_DISTANCE` |

Install the packages you need:

```bash
dotnet add package SemanticDb.Core
dotnet add package SemanticDb.EF
dotnet add package SemanticDb.EF.SqlServer
```

---

## Prerequisites

- .NET 8 or later
- Entity Framework Core 8 or later
- Any `Microsoft.Extensions.AI`-compatible embedding provider (OpenAI, Azure OpenAI, Ollama, etc.)
- **SQL Server 2025+** for native vector search (`SemanticDb.EF.SqlServer`)
  - For development: `mcr.microsoft.com/mssql/server:2025-latest`
  - For lower versions: use `UseEfCore<TContext>()` with in-memory cosine similarity fallback

---

## Getting Started

### 1. Install packages

```bash
dotnet add package SemanticDb.EF.SqlServer
```

### 2. Register SemanticDb with an embedding provider

Pass the embedding provider directly via `UseEmbeddingsProvider` — any `IEmbeddingGenerator<string, Embedding<float>>` from `Microsoft.Extensions.AI` works:

```csharp
// OpenAI
builder.Services.AddSemanticDb(typeof(PrescriptionSearchable).Assembly)
    .UseEmbeddingsProvider(
        new OpenAIClient(apiKey).GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator())
    .UseSqlServer<AppDbContext>();

// Azure OpenAI
builder.Services.AddSemanticDb(typeof(PrescriptionSearchable).Assembly)
    .UseEmbeddingsProvider(
        new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
            .GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator())
    .UseSqlServer<AppDbContext>();

// Ollama (local) — or any other IEmbeddingGenerator registered in DI
builder.Services.AddOllamaEmbeddingGenerator("nomic-embed-text", new Uri("http://localhost:11434"));
builder.Services.AddSemanticDb(typeof(PrescriptionSearchable).Assembly)
    .UseSqlServer<AppDbContext>(); // falls back to the unkeyed IEmbeddingGenerator in DI
```

Pass any number of assemblies to scan for `ISearchableEntity<T>` implementations.

Optionally configure global options:

```csharp
builder.Services.AddSemanticDb(
    options =>
    {
        options.MaxRetries = 5;
        options.RetryBaseDelay = TimeSpan.FromSeconds(10);
        options.DefaultSearchLimit = 25;
    },
    typeof(PrescriptionSearchable).Assembly)
    .UseSqlServer<AppDbContext>();
```

### 3. Configure your DbContext

```csharp
public class AppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplySemanticDbConfiguration();  // registers SemanticDb tables
        builder.UseSqlServerVectorSearch();           // configures VECTOR(1536) column
    }
}
```

Attach the interceptor to have SemanticDb index changes automatically whenever `SaveChangesAsync` is called:

```csharp
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(connectionString)
           .AddSemanticDbInterceptors(sp));
```

If you prefer to control indexing entirely yourself — for example, when your application writes primarily through raw SQL or bulk operations — you can skip `AddSemanticDbInterceptors` and use `ISemanticDbIndexer.RequestReindexAsync` directly instead. See [Manual re-indexing](#manual-re-indexing-with-isemanticdbindexer).

### 4. Add and run migrations

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

EF Core will automatically generate the correct `VECTOR(1536)` column type in the migration — no manual edits required.

---

## Defining Searchable Entities

Implement `ISearchableEntity<T, TScopeKey>` in a dedicated class — never on the entity itself.

```csharp
public class CardsBySetSearchable : ISearchableEntity<Card, string>
{
    // Text sent to the embedding model — write natural prose for best results
    public string ToSearchContent(Card entity) =>
        $"{entity.Name} is a {entity.ManaCost} mana {entity.Type}. {entity.OracleText}";

    // Optional: override to return richer context for LLM consumption
    // Defaults to ToSearchContent if omitted
    public string ToPromptContext(Card entity) =>
        $"**{entity.Name}** ({entity.ManaCost})\n{entity.Type}\n{entity.OracleText}";

    // Optional: restrict search to a subset (e.g. by set, by player collection)
    public object? GetScopeKey(Card entity) => entity.SetCode;

    // Increment when ToSearchContent changes to trigger automatic re-indexing
    public int Version => 1;
}
```

You can register multiple `ISearchableEntity<T, TScopeKey>` implementations for the same entity type — each produces an independent index under its own name:

```csharp
public class CardsByColorSearchable : ISearchableEntity<Card, string> { ... }
public class CardsBySetSearchable   : ISearchableEntity<Card, string> { ... }

// Search against a specific index by type
var results = await search.SearchAsync<CardsByColorSearchable>("blue card draw spell");
var results = await search.SearchAsync<CardsBySetSearchable>("Innistrad horror creature");
```

### Interface reference

| Member | Required | Description |
|---|---|---|
| `ToSearchContent(T)` | Yes | Text indexed and embedded for search |
| `ToPromptContext(T)` | No | Text stored alongside the embedding and returned on results (defaults to `ToSearchContent`) |
| `GetScopeKey(T)` | No | Partition key for scoped search; the return type is `TScopeKey?` and enforced at the `SearchAsync` call site (default: `null`) |
| `Version` | No | Incremented to trigger re-indexing (default: `1`) |

> The chunk name is derived from the implementation class name: `CardsBySetSearchable` → `"CardsBySetSearchable"`. This is what is stored in the database.

---

## Configuration

| Option | Default | Description |
|---|---|---|
| `MaxRetries` | `3` | Maximum retry attempts for a failed outbox entry |
| `RetryBaseDelay` | `5s` | Base delay for exponential backoff between retries |
| `DefaultSearchLimit` | `25` | Default number of results returned by `SearchAsync` |

---

## Versioning and Re-indexing

### Automatic re-indexing on version change

When `ToSearchContent` changes, the embedded text is stale. Increment `Version` to trigger automatic re-indexing at the next application startup:

```csharp
public int Version => 2; // was 1
```

At startup, SemanticDb compares the stored version in `RagIndexState` with the current version. If they differ, it enqueues a full re-index for that entity type via the outbox. The operation is safe under horizontal scaling — only one instance will perform the re-index per version change.

Re-indexing is processed asynchronously by the background outbox processor and does not block application startup.

### Manual re-indexing with `ISemanticDbIndexer`

The EF Core interceptor only fires when changes go through `SaveChangesAsync`. If your application modifies entities via raw SQL, bulk operations, or an external system, those changes will not be indexed automatically. Use `ISemanticDbIndexer` to trigger indexing manually:

```csharp
// Inject ISemanticDbIndexer
public class ProductService(AppDbContext db, ISemanticDbIndexer indexer)
{
    // Re-index a single entity — use when you know which entity changed
    public async Task UpdateDescriptionAsync(int id, string newDescription)
    {
        await db.Products
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Description, newDescription));

        await indexer.RequestReindexAsync<Product>(id);
    }

    // Re-index all entities of a type — use after bulk operations or data migrations
    public async Task RebuildIndexAsync()
    {
        await indexer.RequestReindexAsync<Product>();
    }
}
```

Both overloads enqueue work via the outbox and return immediately — the actual embedding generation happens asynchronously in the background. If an unclaimed entry already exists for the same entity, it is reset to pending rather than duplicated.

---

## Using Search Results with an LLM

`SearchAsync` returns `SemanticDbResult` objects that each carry a `PromptContext` field — text pre-rendered at index time specifically for LLM consumption, requiring no extra database round-trip. You own the prompt and the call, which means full control over conversation history, streaming, tool use, and output format.

### One-shot question answering

```csharp
var results = await semanticSearch.SearchAsync<PrescriptionSearchable, int>(
    query: "blood pressure medication",
    scopeKey: patientId);

var context = string.Join("\n\n", results.Select(r => r.PromptContext));

var response = await chatClient.GetResponseAsync([
    new(ChatRole.System,
        "You are a medical assistant. Answer based solely on the patient's prescriptions below. " +
        "If the answer is not in the context, say so.\n\n" +
        $"Prescriptions:\n{context}"),
    new(ChatRole.User, "What medications is the patient currently taking for blood pressure?")
]);

Console.WriteLine(response.Text);
```

### Multi-turn conversational RAG

Maintain conversation history yourself and re-run the search on each user turn to keep the context fresh:

```csharp
var history = new List<ChatMessage>
{
    new(ChatRole.System,
        "You are a helpful assistant. Answer based solely on the context provided in each user message. " +
        "If the answer is not in the context, say so.")
};

while (true)
{
    var question = Console.ReadLine()!;

    // Re-retrieve for every turn — keeps context relevant as the conversation evolves
    var results = await semanticSearch.SearchAsync<PrescriptionSearchable, int>(
        query: question,
        scopeKey: patientId);

    var context = string.Join("\n\n", results.Select(r => r.PromptContext));

    // Inject retrieved context alongside the user's question
    history.Add(new(ChatRole.User, $"Context:\n{context}\n\nQuestion: {question}"));

    var response = await chatClient.GetResponseAsync(history);
    history.Add(new(ChatRole.Assistant, response.Text));

    Console.WriteLine(response.Text);
}
```

---

## Using an Alternative Provider (without SQL Server)

If you are not using SQL Server 2025, or want to implement your own vector store, use `UseEfCore<TContext>()` instead of `UseSqlServer<TContext>()`. This registers an in-memory cosine similarity fallback suitable for development or low-volume scenarios:

```csharp
builder.Services.AddSemanticDb(typeof(PrescriptionSearchable).Assembly)
    .UseEfCore<AppDbContext>();
```

You can also implement `IVectorSearch` directly to plug in any vector database (PostgreSQL pgvector, Azure AI Search, etc.):

```csharp
builder.Services.AddScoped<IVectorSearch, MyCustomVectorSearch>();
```

---

## Architecture

```
Your Application
       │
       ├── EF Core SaveChanges
       │        │
       │   RagInterceptor ──────────────────► RagOutbox (same transaction)
       │
       └── RagOutboxProcessor (BackgroundService)
                │
                ├── Claim batch (UPDLOCK, READPAST — multi-instance safe)
                ├── Load entities from DB (batched)
                ├── Generate embeddings (IEmbeddingGenerator, parallel, capped concurrency)
                ├── Upsert RagChunks
                └── Delete processed outbox entries

ISemanticDbService
       │
       ├── Generate query embedding (IEmbeddingGenerator)
       └── IVectorSearch
               ├── InMemoryVectorSearch   (EF fallback, cosine similarity)
               └── SqlServerVectorSearch  (VECTOR_DISTANCE, SQL Server 2025)
```

**Tables created by the library:**

| Table | Description |
|---|---|
| `RagOutbox` | Pending indexing and deletion operations |
| `RagChunks` | Stored embeddings and metadata |
| `RagIndexState` | Tracks the indexed version per entity type |

---

## Limitations and Gotchas
### EF Core interceptor scope
SemanticDb detects changes through an EF Core SaveInterceptor. This means only operations that go through SaveChangesAsync() on a tracked DbContext will trigger indexing. The following patterns will silently bypass the outbox:

 ```csharp
 // None of these will trigger indexing:
await context.Database.ExecuteSqlRawAsync("UPDATE Prescriptions SET ...");
await context.Prescriptions.ExecuteUpdateAsync(...); 
await context.Prescriptions.ExecuteDeleteAsync(...);  
// Dapper, ADO.NET, or any bulk insert library on the same DB
```

For these cases, use `ISemanticDbIndexer.RequestReindexAsync` to trigger indexing manually. See [Versioning and Re-indexing](#versioning-and-re-indexing).


---

## Roadmap

- [ ] PostgreSQL + pgvector provider (`SemanticDb.EF.Postgres`) — highest priority
- [ ] Polling sync strategy for microservice / cross-process indexing
- [ ] Support for multi-entity chunks (combine multiple tables into one chunk)
- [ ] OpenTelemetry metrics integration

---

## Contributing

Contributions are welcome. Please open an issue before submitting a pull request for significant changes.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes
4. Open a pull request against `main`

Please follow the existing code style and ensure all tests pass before submitting.

---

## License

MIT — see [LICENSE](LICENSE) for details.
