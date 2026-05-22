# AGENTS.md

Read `CODEBASE.md` first before doing anything in this codebase. It contains the current codebase map, architecture notes, security-sensitive areas, and implementation nuances.

This file provides guidance to coding agents when working in this repository.

## Project Overview

**Universe** (aka `UniverseQuery`) is a C# library that simplifies querying Azure Cosmos DB by providing a fluent, type-safe query builder. It wraps the Cosmos DB SDK with an intuitive abstraction layer that reduces boilerplate while maintaining performance and flexibility.

Key concepts:

- **Galaxy**: Base repository class for querying Cosmos DB containers.
- **CosmicEntity**: Base class/interface for Cosmos DB documents with standard fields (`id`, `AddedOn`, `ModifiedOn`).
- **Catalyst**: Represents a single `WHERE` clause condition.
- **Cluster**: Groups multiple catalysts together with `AND` / `OR` logic.
- **Gravity**: Response object containing RU consumption, continuation tokens, and query debug info.
- **Query Builder**: Internal system that constructs parameterized SQL queries with SQL injection protection.

## Solution Structure

```text
code/
|-- UniverseQuery.slnx
|-- Universe/
|   |-- Galaxy.cs
|   |-- GalaxyBasic.cs
|   |-- GalaxyCore.cs
|   |-- GalaxyProcedures.cs
|   |-- Builder/
|   |-- Interfaces/
|   |-- Extensions/
|   |-- Response/
|   `-- Exception/
|-- Universe.Tests/
`-- DarkMatter/
```

`Universe` is the package project, `Universe.Tests` is the unit test project, and `DarkMatter` is the runnable example/test harness.

## Development Commands

```bash
cd code
dotnet restore
dotnet build
dotnet test
```

Run examples:

```bash
cd code/DarkMatter
dotnet run
```

Release package:

```bash
cd code/Universe
dotnet pack -c Release
```

## Architecture Highlights

### Query Building

- `Builder/QueryBuilder.cs` is the core query-construction file and is security-sensitive.
- Values must be passed through `QueryDefinition.WithParameter`.
- Column names, aliases, joins, groups, sorts, and other identifiers must be validated before being inserted into SQL.
- Identifier paths are rendered with bracket notation, for example `c["metadata"]["sku"]`.
- `Example14_SQLInjectionProtection.cs` demonstrates injection-protection expectations.

### Query Execution Strategies

- `DirectQueryStrategy`: standard simple query execution.
- `GatewayQueryStrategy`: conservative fallback for complex queries.
- `VectorSearchStrategy`: vector and hybrid search execution.
- `QueryStrategySelector`: chooses the best strategy by priority and optional forced hints.
- `QueryTuner`: records execution statistics and recommends hints.

### Query Optimization

- Optional statistics persistence is configured through `UniverseOptions`.
- Storage backends include in-memory, JSON file, and SQLite.
- File and SQLite custom paths must not come from untrusted input.
- Query statistics store structural hashes and performance metadata rather than raw query values.

### Special Features

- Vector search via `VectorDistance`.
- Full-text search via Cosmos full-text operators.
- Hybrid vector/full-text ranking via RRF.
- Aggregations with `COUNT`, `SUM`, `MIN`, `MAX`, and `AVG`.
- Pagination through continuation tokens.
- Bulk operations with Cosmos `AllowBulkExecution`.
- Stored procedure lifecycle operations.

### Partition Keys

- Use `[PartitionKey]` on entity properties.
- Up to three partition-key levels are supported.
- Sequence values define multi-level partition-key order.
- `typeof(MyModel).BuildPartitionKey()` builds partition-key paths.
- `model.BuildPartitionKey()` builds runtime partition-key values.

## Important Conventions

- Keep the astronomy/domain terminology already used by the project.
- Public API changes affect the NuGet package; keep them deliberate and tested.
- Use `UniverseException` for library validation failures.
- Preserve parameterized query construction.
- Add tests for query SQL generation changes.
- Add injection-focused tests when adding any new SQL-fragment surface.
- `recordQueries: true` includes full query text and parameter values in `Gravity`; use it for debugging only.
- Do not copy `DarkMatter` debug logging patterns into production guidance.

## Serialization

`UniverseSerializer` uses `System.Text.Json` through `JsonObjectSerializer`.

Defaults:

- Preserve property names unless a naming policy is provided.
- Case-insensitive property matching.
- Skip null values on write.
- Skip unmapped members.
- Ignore read-only fields and properties.

When a naming policy is configured on the serializer, query column names are converted with the same policy.

## Security

- SQL injection protection is a core guarantee.
- Query values are parameterized.
- Query identifiers are validated and bracket-formatted.
- `recordQueries` and `GenerateQuery` can expose sensitive query parameters.
- Stored procedure bodies are raw JavaScript strings and should be treated as trusted administrative input only.
- Persistent query-statistics paths should never be derived from user input.

## Target Framework

- Current target: `.NET 10.0`
- C#: `14.0`
- Cosmos SDK: `Microsoft.Azure.Cosmos` `3.58.0`

## Git Workflow

- Main branch: `dev`
- Production branch: `prod`
- Inspect the current branch before making workflow assumptions.
- After every commit or before pushing, run local CodeRabbit review when available:

```bash
coderabbit review
```

## CI/CD

- GitHub Actions restore and build the solution on push.
- NuGet publishing is handled through the package release workflow.
- Semgrep scans run on PRs and commits.

## Documentation

- `CODEBASE.md`: first-read codebase map and nuance document.
- `README.md`: package overview and usage.
- `docs/FLUENT_QUERY_BUILDER.md`: fluent query builder API.
- `docs/DECLARATIVE_QUERY_SYNTAX.md`: declarative query syntax.
- `docs/VECTORDISTANCE_USAGE.md`: vector search.
- `docs/FULLTEXT_USAGE.md`: full-text search.
- `docs/QUERY_EXECUTION_STRATEGIES.md`: strategy and tuning behavior.
- `docs/SQLITE_STATISTICS_STORAGE.md`: SQLite persistence.
- `docs/ADAPTIVE_QUERY_OPTIMIZATION_DESIGN.md`: adaptive optimization design notes.
