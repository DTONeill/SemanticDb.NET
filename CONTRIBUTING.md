# Contributing to SemanticDb

Thank you for your interest in contributing!

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker (for running SQL Server integration tests locally)

## Running the tests

### Unit tests

```bash
dotnet test tests/SemanticDb.Tests
```

### Integration tests (SQLite, no Docker required)

```bash
dotnet test tests/SemanticDb.IntegrationTests
```

### All tests with coverage

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

## Running the sample app

1. Copy `samples/MtgWebApiSample/appsettings.example.json` to `appsettings.json` and fill in your values.
2. Start SQL Server:
   ```bash
   cd samples/MtgWebApiSample
   MSSQL_SA_PASSWORD=YourStrong!Passw0rd docker compose up -d
   ```
3. Run the app:
   ```bash
   dotnet run --project samples/MtgWebApiSample
   ```

## Pull request guidelines

- Keep PRs focused on a single concern.
- Add or update tests for any changed behaviour.
- Update `CHANGELOG.md` under `[Unreleased]` with a brief description.
- Ensure `dotnet build` and `dotnet test` pass before opening a PR.

## Project structure

```
src/
  SemanticDb.Core/          # Abstractions, outbox pipeline, DI wiring
  SemanticDb.EF/            # EF Core stores, interceptor, InMemory vector search
  SemanticDb.EF.SqlServer/  # SQL Server native vector search
tests/
  SemanticDb.Tests/         # Unit tests
  SemanticDb.IntegrationTests/ # End-to-end pipeline tests (SQLite)
samples/
  MtgWebApiSample/              # Full sample — MTG card search with SQL Server
```

## Coding conventions

- C# nullable reference types are enabled globally — keep all signatures nullable-correct.
- Follow the existing `.editorconfig` style (4-space indent, `var` only when type is obvious, etc.).
- Prefer editing existing files over creating new ones.
- No comments that just restate what the code says; only explain non-obvious intent.
