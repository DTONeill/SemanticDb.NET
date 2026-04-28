# Changelog

All notable changes to this project will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- GitHub Actions CI workflow: build, test, and code coverage on every PR and push to `main`.
- GitHub Actions publish workflow: packs and pushes NuGet packages on `v*.*.*` tags.
- `SemanticDbHealthCheck` — registers as an `IHealthCheck` and reports `Degraded` when permanently-failed outbox entries exist. Wire it up with `services.AddHealthChecks().AddSemanticDb()`.
- `IRagOutboxStore.CountByStatusAsync` for health-check and diagnostic queries.
- Startup validation in `SemanticDbValidationService` for all `SemanticDbOptions` fields (`VectorDimensions`, `MaxRetries`, `DefaultSearchLimit`, `RetryBaseDelay`).
- Structured log scope (`EntryId`, `ChunkName`) on embedding-failure log lines in `RagOutboxProcessor`.
- `appsettings.example.json` in the sample project as a template; `appsettings.json` is now `.gitignore`d.
- Root `.gitignore` covering build output, NuGet artifacts, IDE files, and sample secrets.

---

## [0.1.0] — Initial release

### Added
- `SemanticDb.Core`: core abstractions, outbox pipeline, embedding orchestration, retry/back-off, optimistic concurrency.
- `SemanticDb.EF`: EF Core integration — `RagInterceptor`, EF-backed stores, `InMemoryVectorSearch`.
- `SemanticDb.EF.SqlServer`: SQL Server native vector search via `VECTOR_DISTANCE`.
- `ISearchableEntity<T>` interface for declaring searchable entities.
- Builder pattern for registration: `AddSemanticDb().UseSqlServer()`.
- Automatic re-indexing on version bump of `ISearchableEntity<T>.Version`.
- Multi-tenant support via `GetScopeKey()`.
