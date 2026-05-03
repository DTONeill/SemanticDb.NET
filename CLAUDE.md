# SemanticDb.NET

SemanticDb is a three-package .NET 8 NuGet library that adds semantic (RAG) search to EF Core applications. It intercepts `SaveChanges`, queues embedding jobs through an outbox, and exposes a provider-agnostic `ISemanticDbService` for vector similarity queries. The SQL Server 2025 package uses native vector columns; the base EF package falls back to in-memory cosine search.

---

## Project Structure

```
SemanticSearch.sln
├── src/
│   ├── SemanticDb.Core/          # Core abstractions, outbox pipeline, embedding orchestration
│   ├── SemanticDb.EF/            # EF Core integration: interceptor, stores, in-memory search
│   └── SemanticDb.EF.SqlServer/  # SQL Server 2025 native vector search
├── tests/
│   ├── SemanticDb.Tests/             # xUnit unit tests (Moq, no I/O)
│   ├── SemanticDb.IntegrationTests/  # End-to-end pipeline tests (SQLite, no Docker)
│   └── MtgWebApiSample.Tests/        # Sample app tests (WebApplicationFactory)
├── samples/
│   ├── MtgWebApiSample.Core/
│   └── MtgWebApiSample.SqlServer/
└── Directory.Build.props             # Shared version, nullable, implicit usings
```

---

## Build & Test Commands

```bash
# Restore
dotnet restore

# Build
dotnet build --configuration Release

# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/SemanticDb.Tests

# Run integration tests only (SQLite — no external dependencies)
dotnet test tests/SemanticDb.IntegrationTests

# Pack all three NuGet packages
dotnet pack src/SemanticDb.Core          --configuration Release --output ./artifacts
dotnet pack src/SemanticDb.EF            --configuration Release --output ./artifacts
dotnet pack src/SemanticDb.EF.SqlServer  --configuration Release --output ./artifacts
```

---

## Bug Fix Workflow

**Every bug fix MUST follow this sequence. No exceptions.**

### 1. Write a failing test first

Before touching any production code, add a test that reproduces the exact defect. Place it in the appropriate project:

- `tests/SemanticDb.Tests` — unit-level bugs (pure logic, no I/O)
- `tests/SemanticDb.IntegrationTests` — pipeline/EF bugs (uses SQLite in-process)

Name the test to describe the broken behaviour, not the fix:

```csharp
[Fact]
public async Task RagOutboxProcessor_ShouldRetry_WhenEmbeddingGeneratorThrowsTransientException()
{
    // Arrange: reproduce the exact conditions that trigger the bug
    // Act
    // Assert: what should happen (currently broken)
}
```

### 2. Verify the test fails

```bash
dotnet test tests/SemanticDb.Tests --filter "FullyQualifiedName~<TestName>"
```

The test **must be red** before you write any fix. If it passes against unfixed code, the test does not reproduce the bug — revise it.

### 3. Fix the production code

Make the smallest change that makes the test pass. Do not refactor unrelated code in the same commit.

### 4. Verify all tests pass

```bash
dotnet test
```

All tests must be green before the fix is complete.

---

## Coding Conventions

Follow `.editorconfig` and the existing codebase patterns.

**Types & nullability**
- Nullable reference types are enabled everywhere. Annotate all public APIs. Never suppress with `!` without a comment explaining why.
- Predefined aliases only: `int`, `string`, `bool` — not `Int32`, `String`, `Boolean`.
- `var` only when the right-hand side makes the type obvious (e.g. `var list = new List<string>()`).

**Naming**
- Interfaces: `I` prefix — `IVectorSearch`, `ISearchableEntity<T, TScopeKey>`.
- Internal implementations: mark `sealed`.
- Async methods: always suffix with `Async`.
- No `this.` prefix on members.

**Formatting**
- 4-space indentation; no tabs.
- Always use braces, even for single-line `if`/`else`.
- Expression-bodied members only for genuine single-expression bodies.
- Prefer pattern matching over type-checking chains.

**Documentation**
- XML doc comments on every public type and member.
- Comments explain *why*, never *what*. Remove any comment that merely restates the code.

**DI & configuration**
- Extend via builder pattern: `AddSemanticDb()` returns a `SemanticDbBuilder`.
- Register options through `IOptions<T>`; never read configuration directly in domain types.

---

## Architecture Notes

### Key abstractions (`SemanticDb.Core`)

| Abstraction | Role |
|---|---|
| `ISearchableEntity<T, TScopeKey>` | Entity opt-in: provides `ToSearchContent()` and `ToPromptContext()` |
| `ISemanticDbService` | Main search API consumed by application code |
| `IVectorSearch` | Pluggable vector store (swap SQL Server for in-memory or custom) |
| `IEmbeddingGenerator<string, Embedding<float>>` | Microsoft.Extensions.AI contract; inject any model provider |

### Outbox pipeline (`SemanticDb.EF`)

```
SaveChanges
  └── RagInterceptor         (detects ISearchableEntity changes)
        └── RagOutbox        (enqueues indexing job in the same DbContext transaction)
              └── RagOutboxProcessor  (IHostedService — dequeues & calls IEmbeddingGenerator)
                    ├── RagChunks      (stores embedding vectors)
                    └── RagIndexState  (tracks per-entity index version)
```

The outbox guarantees embedding jobs survive application crash: the job is written in the same transaction as the entity change and processed by the background service after commit.

### Multi-tenancy / scoping

Entities are scoped by `TScopeKey` on `ISearchableEntity<T, TScopeKey>`. Pass the scope key to `ISemanticDbService.SearchAsync` to isolate results per tenant, user, or context.

### Provider model

- `SemanticDb.EF` — provider-agnostic; uses in-memory cosine similarity by default.
- `SemanticDb.EF.SqlServer` — overrides `IVectorSearch` with SQL Server 2025 native `VECTOR_DISTANCE` queries.

---

## Release & Versioning

Versions are managed with git tags and propagated via `Directory.Build.props`.

```bash
git tag v1.2.0
git push origin v1.2.0
```

Pushing a `v*.*.*` tag triggers `publish.yml`, which packs all three packages and publishes them to NuGet.org using the `NUGET_API_KEY` repository secret.

**Do not manually edit version numbers in `.csproj` files.** The version is set centrally in `Directory.Build.props` and injected from the tag by the CI pipeline.

CI runs on every push to `main` and on every PR:

```
dotnet restore → dotnet build → dotnet test --collect:"XPlat Code Coverage"
```

A PR cannot be merged if any test fails or the build is broken.
