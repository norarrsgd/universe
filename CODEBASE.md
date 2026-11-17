# CODEBASE.md

Read this file first before doing anything in this codebase. It is the working map for the repository, its architecture, and the implementation nuances that are easy to miss from file names alone.

## Project Identity

Universe, published as `UniverseQuery`, is a C#/.NET library for querying Azure Cosmos DB through repository classes and a fluent, type-oriented query builder.

The library is centered on these concepts:

- `Galaxy`: repository abstraction for one Cosmos DB container.
- `GalaxyBasic`: basic create, read, update, delete, and bulk operations.
- `GalaxyProcedure`: stored procedure lifecycle and execution operations.
- `CosmicEntity` / `ICosmicEntity`: base document contract.
- `Orbit<T>`: fluent query builder exposed through `galaxy.Query()`.
- `Cluster` and `Catalyst`: declarative query-condition data structures.
- `Gravity`: operation metadata returned from every public operation, including request units, continuation token, and optional query details.
- `UniverseBuilder`: internal SQL builder and query execution dispatcher.
- `QueryTuner`: adaptive query-statistics collector and recommendation source.

## Repository Map

```text
.
|-- CLAUDE.md
|-- AGENTS.md
|-- CODEBASE.md
|-- README.md
|-- VERSION
|-- docs/
|   |-- ADAPTIVE_QUERY_OPTIMIZATION_DESIGN.md
|   |-- DECLARATIVE_QUERY_SYNTAX.md
|   |-- FLUENT_QUERY_BUILDER.md
|   |-- FULLTEXT_USAGE.md
|   |-- QUERY_EXECUTION_STRATEGIES.md
|   |-- ROADMAP_STATUS.md
|   |-- SQLITE_STATISTICS_STORAGE.md
|   `-- VECTORDISTANCE_USAGE.md
`-- code/
    |-- UniverseQuery.slnx
    |-- Universe/
    |-- Universe.Tests/
    `-- DarkMatter/
```

## Solution Map

```text
code/
|-- UniverseQuery.slnx
|-- Universe/
|   |-- UniverseQuery.csproj
|   |-- GalaxyCore.cs
|   |-- GalaxyBasic.cs
|   |-- Galaxy.cs
|   |-- GalaxyProcedures.cs
|   |-- DocumentCacheOptions.cs
|   |-- UniverseOptions.cs
|   |-- Builder/
|   |   |-- QueryBuilder.cs
|   |   |-- Orbit.cs
|   |   |-- ClusterBuilder.cs
|   |   |-- Caching/
|   |   |-- Options/
|   |   `-- Strategies/
|   |-- Interfaces/
|   |-- Attributes/
|   |-- Extensions/
|   |-- Response/
|   `-- Exception/
|-- Universe.Tests/
|   |-- Builder/
|   |-- Storage/
|   `-- Tuner/
`-- DarkMatter/
    |-- Program.cs
    |-- Examples/
    |-- Models/
    |-- Repository/
    `-- Helpers/
```

## Complete Source Inventory

### `code/Universe`

The `Universe` project is the library package. Anything in this project can become part of the NuGet package surface directly or indirectly, so preserve binary/API compatibility unless the change is intentionally breaking.

```text
code/Universe/
|-- Attributes/
|   `-- PartitionKeyAttribute.cs
|-- Builder/
|   |-- ClusterBuilder.cs
|   |-- Orbit.cs
|   |-- QueryBuilder.cs
|   |-- Options/
|   |   |-- AggregationOption.cs
|   |   |-- ColumnOptions.cs
|   |   |-- JoinOptions.cs
|   |   |-- Parameters.cs
|   |   |-- QueryHints.cs
|   |   |-- QueryOptions.cs
|   |   |-- SortingOptions.cs
|   |   |-- UniverseSerializerOptions.cs
|   |   `-- WhereClause.cs
|   `-- Strategies/
|       |-- DirectQueryStrategy.cs
|       |-- EnhancedContextStrategy.cs
|       |-- GatewayQueryStrategy.cs
|       |-- IQueryExecutionStrategy.cs
|       |-- QueryContext.cs
|       |-- QueryExecutionStatistics.cs
|       |-- QueryStrategySelector.cs
|       |-- QueryTuner.cs
|       |-- QueryTypeDetector.cs
|       |-- VectorSearchStrategy.cs
|       `-- Storage/
|           |-- FileStatisticsStorage.cs
|           |-- InMemoryStatisticsStorage.cs
|           |-- IQueryStatisticsStorage.cs
|           |-- PlatformDetection.cs
|           `-- SqliteStatisticsStorage.cs
|-- Exception/
|   `-- UniverseException.cs
|-- Extensions/
|   |-- HintValueExtensions.cs
|   |-- OrbitExtensions.cs
|   |-- PartitionKeyExtension.cs
|   |-- ProjectionColumnExtractor.cs
|   `-- StrExtensions.cs
|-- Interfaces/
|   |-- ICosmicEntity.cs
|   |-- IGalaxy.cs
|   |-- IGalaxyBasic.cs
|   `-- IGalaxyProcedure.cs
|-- Response/
|   `-- Response.cs
|-- Galaxy.cs
|-- GalaxyBasic.cs
|-- GalaxyCore.cs
|-- GalaxyProcedures.cs
|-- DocumentCacheOptions.cs
|-- UniverseOptions.cs
|-- UniverseQuery.csproj
|-- UniverseQuery.xml
|-- LICENSE
`-- global-usings.cs
```

File responsibilities:

- `Attributes/PartitionKeyAttribute.cs`: attribute for marking partition-key properties. It supports default sequence `1`, validates sequence `1` through `3`, and optionally accepts an explicit Cosmos key name.
- `Builder/Caching/DocumentCache.cs`: optional process-local document cache used only when `UniverseOptions.WithDocumentCache(...)` is configured.
- `Builder/ClusterBuilder.cs`: fluent builder for one `Cluster`. It stores catalysts in order and tracks the next intra-cluster `AND` / `OR` connector.
- `Builder/Orbit.cs`: public fluent query builder. It accumulates clusters, selected columns, sort options, aggregates, group-by columns, pagination, joins, hints, and execution mode.
- `Builder/QueryBuilder.cs`: internal SQL-generation and query-dispatch core. Treat as security-sensitive.
- `Builder/QueryParameterNormalizer.cs`: shared quote-aware SQL parameter normalization for cache signatures and statistics hashes. Returns token names in SQL occurrence order, including repeats.
- `Builder/Options/AggregationOption.cs`: record struct pairing a column with a `Q.Aggregate` enum.
- `Builder/Options/ColumnOptions.cs`: record struct for selected columns, `DISTINCT`, `TOP`, aggregates, and joins.
- `Builder/Options/JoinOptions.cs`: record for Cosmos array `JOIN` configuration.
- `Builder/Options/Parameters.cs`: `Catalyst` definition and rule validation; also generates parameter names.
- `Builder/Options/QueryHints.cs`: user-facing query hint record and execution strategy enum.
- `Builder/Options/QueryOptions.cs`: `Q` operator, aggregate, page, and where-option definitions plus SQL keyword conversion helpers.
- `Builder/Options/SortingOptions.cs`: sorting option and sort-direction keyword conversion.
- `Builder/Options/UniverseSerializerOptions.cs`: Cosmos serializer implementation backed by `System.Text.Json`.
- `Builder/Options/WhereClause.cs`: `Cluster` record struct.
- `Builder/Strategies/DirectQueryStrategy.cs`: simple-query executor.
- `Builder/Strategies/GatewayQueryStrategy.cs`: conservative fallback executor.
- `Builder/Strategies/VectorSearchStrategy.cs`: vector, full-text score, and hybrid-query executor.
- `Builder/Strategies/EnhancedContextStrategy.cs`: wrapper that supplies merged recommendation/user hints to the selected strategy.
- `Builder/Strategies/IQueryExecutionStrategy.cs`: internal strategy contract.
- `Builder/Strategies/QueryContext.cs`: query type and recommendation DTOs.
- `Builder/Strategies/QueryExecutionStatistics.cs`: recorded performance/statistics model.
- `Builder/Strategies/QueryStrategySelector.cs`: strategy selection and hint merging.
- `Builder/Strategies/QueryTuner.cs`: in-memory statistics window, persistence coordination, and hint recommendation logic.
- `Builder/Strategies/QueryTypeDetector.cs`: derives query type and ranking/grouping flags from validated catalysts, column options, and final grouping; retains the scalar SQL token-count complexity threshold.
- `Builder/Strategies/Storage/IQueryStatisticsStorage.cs`: storage abstraction.
- `Builder/Strategies/Storage/InMemoryStatisticsStorage.cs`: volatile statistics storage.
- `Builder/Strategies/Storage/FileStatisticsStorage.cs`: JSON statistics persistence.
- `Builder/Strategies/Storage/SqliteStatisticsStorage.cs`: SQLite statistics persistence with WAL and batched writes.
- `Builder/Strategies/Storage/PlatformDetection.cs`: Azure/local path selection, path validation, and Unix permission tightening.
- `Exception/UniverseException.cs`: library exception.
- `Extensions/HintValueExtensions.cs`: safe conversion of hint values to int/bool.
- `Extensions/OrbitExtensions.cs`: exposes `.Query()` on `IGalaxy<T>`.
- `Extensions/PartitionKeyExtension.cs`: builds Cosmos partition-key paths and runtime values from attributes.
- `Extensions/ProjectionColumnExtractor.cs`: extracts projection property names and honors `[JsonIgnore]`.
- `Extensions/StrExtensions.cs`: string naming helpers, especially lower-camel conversion.
- `GalaxyCore.cs`: Cosmos database/container initialization and shared flags.
- `GalaxyBasic.cs`: basic document operations and bulk transaction batch operations.
- `Galaxy.cs`: advanced query operations and generated-query inspection.
- `GalaxyProcedures.cs`: stored procedure operations.
- `DocumentCacheOptions.cs`: opt-in document-cache configuration and cache ownership.
- `Interfaces/*.cs`: public contracts.
- `Response/Response.cs`: `Gravity` response record.
- `UniverseOptions.cs`: user-facing query-statistics persistence, document-cache, and repository provisioning configuration.
- `global-usings.cs`: shared namespace imports.

### `code/Universe.Tests`

The test project is the fastest feedback loop for library behavior that does not need a live Cosmos account.

```text
code/Universe.Tests/
|-- Batch/
|   |-- BatchBuilderTests.cs
|   |-- BatchExecutionTests.cs
|   `-- LegacyInterfaceCompatibilityTests.cs
|-- Builder/
|   |-- NamingPolicyQueryTests.cs
|   |-- OrbitQueryTests.cs
|   |-- QueryTypeDetectorTests.cs
|   `-- ProjectionSelectTests.cs
|-- Caching/
|   |-- DocumentCacheRepositoryTests.cs
|   `-- DocumentCacheTests.cs
|-- Extensions/
|   `-- PartitionKeyExtensionTests.cs
|-- Helpers/
|   |-- InMemoryCosmosContainer.cs
|   `-- TestStatisticsFactory.cs
|-- Storage/
|   |-- FileStatisticsStorageTests.cs
|   |-- InMemoryStatisticsStorageTests.cs
|   |-- PlatformDetectionTests.cs
|   `-- SqliteStatisticsStorageTests.cs
|-- Tuner/
|   |-- QueryTunerPerformanceTests.cs
|   `-- QueryTunerTests.cs
`-- Universe.Tests.csproj
```

Test intent:

- Query text generation should be stable after normalizing generated parameter names.
- Fluent `Orbit<T>` should mirror declarative `Cluster` / `Catalyst` output.
- Naming policy changes must be reflected in generated SQL.
- Projection extraction should include expected public properties and exclude ignored/computed fields.
- Storage backends should persist, load, clean up, and tolerate corrupt/tampered storage where applicable.
- Tuner tests verify rule-based fallbacks, data-driven recommendations, persistence loading, and query-hash normalization.
- Performance tests are excluded in CI with `--filter "Category!=Performance"`.

### `code/DarkMatter`

`DarkMatter` is the example harness. It is not the package, but it is a useful compatibility and demonstration surface.

```text
code/DarkMatter/
|-- DarkMatter.csproj
|-- Program.cs
|-- Examples/
|   |-- ExampleBase.cs
|   |-- Example1_BasicPagedQuery.cs
|   |-- Example2_AggregatesWithGroupBy.cs
|   |-- Example3_TopAndDistinct.cs
|   |-- Example4_ComplexFiltering.cs
|   |-- Example5_SingleItemOperations.cs
|   |-- Example6_BulkOperations.cs
|   |-- Example7_AdvancedAggregation.cs
|   |-- Example8_SalesAnalysis.cs
|   |-- Example9_VectorSearch.cs
|   |-- Example10_FullTextSearch.cs
|   |-- Example11_HybridVectorFullText.cs
|   |-- Example12_QueryGeneration.cs
|   |-- Example13_QueryOptimization.cs
|   |-- Example14_SQLInjectionProtection.cs
|   |-- Example15_ContainsOperator.cs
|   `-- Example16_ProjectionSelect.cs
|-- Helpers/
|   |-- TestDataGenerator.cs
|   `-- VectorDataGenerator.cs
|-- Models/
|   |-- MyObject.cs
|   |-- MyObjectAggregation.cs
|   `-- MyObjectVector.cs
`-- Repository/
    |-- MyRepo.cs
    `-- MyRepoVector.cs
```

Nuances:

- `Program.cs` contains placeholder Cosmos URI/key values. Do not commit real secrets there.
- `MyRepo` uses `UniverseOptions.WithFilePersistence()` and `recordQueries: true`, intentionally surfacing queries for examples.
- Example output prints query text and parameters. This is useful for docs and demos, but production guidance should keep `recordQueries` off.
- Vector/full-text examples depend on Cosmos DB account features and proper indexing/container setup.
- `Example14_SQLInjectionProtection.cs` is the living demonstration of the expected injection defenses.

### Root And Documentation Files

- `CODEBASE.md`: this first-read map.
- `AGENTS.md`: agent instructions for Codex and other coding agents.
- `CLAUDE.md`: Claude-specific instructions; should stay aligned with `AGENTS.md`.
- `README.md`: package-facing quickstart and API overview.
- `VERSION`: repository/package version marker.
- `LICENSE`: root license.
- `docs/*.md`: feature-specific guides. Keep docs updated when public API or behavior changes.

### `.github`

```text
.github/
|-- CODEOWNERS
|-- dependabot.yml
|-- trafico.yml
`-- workflows/
    |-- dotnet-ci.yml
    |-- nuget-publish.yml
    |-- semgrep--pr-ci.yml
    `-- semgrep-ci.yml
```

Nuances:

- `dotnet-ci.yml` runs on every push and has restore, build, and test jobs.
- CI uses `.NET 10.x` preview via `actions/setup-dotnet`.
- CI test command excludes tests tagged `Category=Performance`.
- `nuget-publish.yml` runs on pushes to `prod` and `beta`, builds, tests, packs `code/Universe/UniverseQuery.csproj`, and pushes to NuGet using OIDC Trusted Publishing through `NuGet/login`. Repository secret `NUGET_USER` contains the nuget.org profile username, and the publish job requires `id-token: write`. Configure a nuget.org Trusted Publishing policy for GitHub owner `norarrsgd`, repository `universe`, workflow filename `nuget-publish.yml`, and package `UniverseQuery`; leave the policy environment empty because the workflow does not use one. The temporary API key is requested immediately before publishing; `NUGET_KEY` is no longer used.
- Semgrep runs on PRs and on pushes to `dev` and `prod`. All workflow actions use full commit SHAs; Semgrep 1.185.0 uses an immutable container digest and the upstream C# rules snapshot at `a5ad1714f00c9f7fde980cab0ea1d6dc6d414fd2`. The rules checkout is excluded from targets. Scanner and rule updates require reviewed pin changes; workflows do not install the latest package or load `--config auto`.
- `dependabot.yml` schedules daily NuGet updates for `code/Universe`, `code/Universe.Tests`, and `code/DarkMatter`.
- `CODEOWNERS` covers the active `/code/` tree, Markdown documentation, and `.github/` configuration.

## Build And Test

Primary commands:

```bash
cd code
dotnet restore
dotnet build
dotnet test
```

Release/package commands:

```bash
cd code
dotnet build -c Release
cd Universe
dotnet pack -c Release
```

Example harness:

```bash
cd code/DarkMatter
dotnet run
```

`DarkMatter` requires real Cosmos DB values in `Program.cs` before it can run against a live account.

## Target Runtime And Packages

The current package version is `3.5.2-preview.3` (prerelease), declared in `VERSION` and `code/Universe/UniverseQuery.csproj`.

The main library currently targets:

- .NET `net10.0`
- C# `14.0`
- Nullable disabled
- `Microsoft.Azure.Cosmos` `3.64.0`
- `Microsoft.Data.Sqlite` `11.0.0`
- `Newtonsoft.Json` `13.0.4`
- `SQLitePCLRaw.bundle_e_sqlite3` `3.0.5`
- `SQLitePCLRaw.core` `3.0.5`

`Universe.Tests` is an xUnit test project targeting `net10.0`, using `Microsoft.NET.Test.Sdk` `18.12.0`, `xunit.v3` `4.0.3`, and `xunit.runner.visualstudio` `4.0.1`. The tests are local unit tests for query generation, storage, and tuning behavior; they do not require Cosmos DB unless a new test explicitly adds that dependency.

## Public API Map

### Extended Query And Read Capabilities

- `QueryOperand` is a closed immutable comparison operand with Field, ArrayLength, and Parameter factories. `Catalyst.Compare` and `ClusterBuilder.Compare` support the six equality/ordering comparisons; the right operand can be a parameter, field, or array length. Identifiers on both sides use normal validation/naming policy. Existing Len remains equality against a parameter.
- Catalyst adds IgnoreCase for StartsWith/StringContains/StringEquals and PartialMatch for In/NotIn. IsNull/IsString are unary and bind no values. Partial-object patterns are bound whole; Contains retains caller-collection membership. Existing operator numeric values and positional constructors/deconstruction are preserved.
- `ProjectionBuilder` builds immutable `QueryProjection` snapshots using Field, Object, Aggregate, and Build. `ColumnOptions.Projection` and `Orbit.Project` add explicit output shapes without raw SQL. Output names are exact; source paths use naming policy. The partial `QueryProjectionRenderer` owns nested rendering, rendered-name collision checks, aggregate-derived grouping, and preserved root aggregate/vector-score outputs with existing naming-policy aliases. Grouped/aggregate projections reject ordering/ranking. QueryTypeDetector recognizes nested aggregates.
- Projected GetAsync honors the full shape and hints, retains explicit TOP or defaults to TOP 1, and uses bounded single-result execution. Projected GetAsync rejects Paged. Without Project, GetAsync retains its legacy limited shaping behavior.
- `ReadOptions` and `Orbit.WithConsistency` flow through point reads, full-shape query Get/List, every strategy's typed QueryContext, and the direct paging path. Any explicit level bypasses document-cache reads and writes without changing existing entries. Default reads retain TTL behavior. Invalid enum values and overrides conflicting with a client ReadConsistencyStrategy fail before cache access; Strong requires account support and has no downgrade fallback.
- Existing interface signatures remain; required-argument overloads accept ReadOptions, with default interface methods forwarding only representable legacy calls and rejecting unsupported new capabilities. No shared client/request-options mutation occurs.
- ExtendedPredicateTests, StructuredProjectionTests, and ReadConsistencyTests cover generated SQL, validation, naming, projection composition/classification, execution/options, cache policy, and call isolation. LegacyInterfaceCompatibilityTests also covers old IGalaxy implementations.
- Run `dotnet run --project code/DarkMatter -- --query-capabilities` for a credential-free SQL-only example. It disables provisioning and never executes a Cosmos read.
- `code/global.json` selects Microsoft.Testing.Platform; launch `dotnet test` from `code` to load that setting. A root-directory VSTest invocation does not load it. When restricted environments block MSBuild/test discovery sockets, build with `--disable-build-servers -m:1 -nr:false`, then run `dotnet code/Universe.Tests/bin/Debug/net10.0/Universe.Tests.dll -trait- "Category=Performance"` from the repository root for equivalent non-performance validation. No runner/dependency changes are required.

Primary consumer entry points:

- Derive from `Galaxy<T>` for full query support.
- Derive from `GalaxyBasic<T>` for basic CRUD only.
- Derive from `GalaxyProcedure` for stored procedure support.
- Inject/use `IGalaxy<T>` where advanced queries are needed.
- Inject/use `IGalaxyBasic<T>` where only basic CRUD is needed.
- Call `galaxy.Query()` to use the fluent `Orbit<T>` API.
- Use `Cluster` / `Catalyst` directly for declarative query construction.
- Configure serializer behavior with `new UniverseSerializer(...)` on `CosmosClientOptions.Serializer`.
- Configure tuning persistence with `UniverseOptions.WithFilePersistence(...)` or `UniverseOptions.WithSqlitePersistence(...)`.
- Enable optional process-local document caching with `UniverseOptions.WithDocumentCache(...)`.

Important public contracts:

- `IGalaxyBasic<T>.Create(T)` returns `(Gravity g, string t)`, where `t` is the id.
- `IGalaxyBasic<T>.Create(IReadOnlyList<T>)` returns aggregate `Gravity` for the bulk operation.
- `IGalaxyBasic<T>.Modify(T)` uses `ReplaceItemAsync`, not patch semantics.
- `IGalaxyBasic<T>.Modify(IReadOnlyList<T>)` uses transactional batches grouped by partition key.
- `IGalaxyBasic<T>.Remove(...)` deletes by id and partition key.
- `IGalaxyBasic<T>.Get(...)` reads by id and partition key.
- `IGalaxy<T>.Get(...)` runs a query and returns the first result or default.
- `IGalaxy<T>.List(...)` executes full query shape and can use hints.
- `IGalaxy<T>.Paged(...)` uses continuation tokens.
- `IGalaxy<T>.GenerateQuery(...)` returns query text and parameters without execution.
- `IGalaxy<T>.GetQueryRecommendations(...)` currently ignores the `queryPattern` argument and uses `queryType`.
- `IGalaxyProcedure` methods map directly to Cosmos stored procedure script APIs.

## Runtime Flow

1. Consumer code creates a repository by deriving from `Galaxy<T>` or `GalaxyBasic<T>`.
2. `GalaxyCore` validates database and container names, creates the database/container if missing, captures serializer naming policy, and stores the Cosmos `Container`.
3. Basic CRUD calls go through `GalaxyBasic<T>`.
4. Advanced list/get/paged calls go through `Galaxy<T>`.
5. Query inputs are converted into a `QueryDefinition` by `UniverseBuilder.CreateQuery`.
6. Query execution uses a strategy selected by `QueryStrategySelector`.
7. Execution returns `Gravity` plus the requested model/list/projection result.
8. Query execution statistics are recorded by `QueryTuner` and optionally persisted through `UniverseOptions`.
9. If document caching is explicitly enabled, point reads and single-document query reads check the process-local cache before executing Cosmos requests.

## Detailed Operation Flows

### Repository Construction

`GalaxyCore` constructor behavior:

1. Rejects empty container name.
2. Rejects empty database name.
3. Calls `CreateDatabaseIfNotExistsAsync(database).GetAwaiter().GetResult()`.
4. Creates `ContainerProperties` from the container name and partition-key paths.
5. Stores `_recordQuery`.
6. Reads `AllowBulkExecution` from `CosmosClientOptions`.
7. If the serializer is `UniverseSerializer`, captures its `NamingPolicy`.
8. Calls `CreateContainerIfNotExistsAsync(containerProps).GetAwaiter().GetResult()`.

Nuances:

- Construction performs synchronous blocking waits on async Cosmos calls.
- Repository creation has side effects: database/container creation can happen during DI construction.
- Container partition-key paths come from the repository constructor argument, usually `typeof(T).BuildPartitionKey()`.
- Serializer naming policy is captured once during repository construction.

### Create

`GalaxyBasic<T>.Create(T)` behavior:

1. Assigns a version 7 GUID if `model.id` is blank.
2. Sets `AddedOn = DateTime.UtcNow`.
3. Calls `CreateItemAsync` with `model.BuildPartitionKey()`.
4. Sets `EnableContentResponseOnWrite = false`.
5. Returns request charge and id.
6. Converts conflict into `{T} already exists.`
7. Wraps non-conflict Cosmos errors as `UniverseException` with status code.

### Bulk Create

`GalaxyBasic<T>.Create(IReadOnlyList<T>)` behavior:

1. Requires Cosmos client `AllowBulkExecution`.
2. Serializes the whole model list and rejects payloads over 2 MB.
3. Rejects more than 100 items.
4. Groups models by runtime partition key.
5. Creates one transactional batch per partition key.
6. Assigns ids and `AddedOn`.
7. Uses `EnableContentResponseOnWrite = false`.
8. Executes all batches and sums request charge.

Nuances:

- Despite the `AllowBulkExecution` guard, implementation uses transactional batches grouped by partition key.
- Batch failures are surfaced as `UniverseException`.
- The 2 MB check is on the serialized full list, not per partition group.

### Modify

`GalaxyBasic<T>.Modify(T)` behavior:

1. Sets `ModifiedOn = DateTime.UtcNow`.
2. Calls `ReplaceItemAsync(model, model.id, model.BuildPartitionKey())`.
3. Returns request charge and returned resource.
4. Converts not found into `{T} does not exist.`

Bulk modify mirrors bulk create with `ReplaceItem` operations and the same `AllowBulkExecution`, 2 MB, and 100-item limits.

### Remove

`Remove(id, string partitionKey)` uses a single string `PartitionKey`.

`Remove(id, params string[] partitionKey)` uses `PartitionKeyBuilder` and rejects null/empty partition key arrays and null/blank values.

Both set `EnableContentResponseOnWrite = false` and convert not found into `{T} does not exist.`

### Direct Id Get

`Get(id, string partitionKey)` and `Get(id, params string[] partitionKey)` call `ReadItemAsync` and return request charge plus resource. Not found is converted into `{T} does not exist.`

When `UniverseOptions.WithDocumentCache(...)` is configured, direct id reads are cached by hashed database/container/type/id/partition-key scope. Cache hits return `Gravity.RU = 0`. Without `DocumentCache`, the code path only performs a null check and behaves as before.

### Query Get/List/Paged

`Galaxy<T>.Get(...)`:

- Builds a `TOP 1` query with optional selected columns, including for rank catalysts.
- Executes through `QBuilder.GetOneFromQuery`.
- Marks the `QueryContext` for single-result execution. All strategies force page and buffer sizes to one after hint merging, read past empty pages, retain only the first document, and dispose the iterator without reading further pages.
- Returns first result or `default`.
- When document caching is enabled, actual SQL parameter tokens outside quoted strings/property names are normalized into a hashed cache key. Bound values follow SQL occurrence order, including repeats; unresolved bindings bypass caching. Equivalent queries with newly generated catalyst ids can still share entries, while fields such as `access@a` and `access@b` remain distinct.
- Query keys include parameter runtime types and accept only null, string, boolean, and built-in numeric values (`byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`). Other values bypass both query-cache reads and writes through `Galaxy.TryCreateQueryCacheKey`; do not reuse document-cloning serialization for object-valued cache signatures.

`Galaxy<T>.List(...)`:

- Builds full query shape.
- Uses `QBuilder.GetListFromQuery`.
- Uses the builder's structured `QueryContext`; explicit hints are attached without losing ranking/grouping or execution flags.

`Galaxy<T>.Paged(...)`:

- Builds query.
- Calls `GetItemQueryIterator` directly instead of going through strategy selector.
- Supplies `MaxItemCount = page.Size`.
- Supplies the continuation token if non-blank.
- Rejects grouping using structured `QueryContext.HasGrouping` before opening an iterator; grouped results should use `List`. Reads until the first non-empty page, then returns its continuation token and breaks.
- Returns generated query details only when `_recordQuery` is true.

Nuances:

- Paged queries do not currently record `QueryTuner` statistics.
- Paged queries do not apply `QueryHints`.
- A quoted property containing `GROUP BY` has no effect on paging; control flow does not inspect rendered SQL substrings.

### Generate Query

`GenerateQuery` always returns `Gravity` containing:

- `RU = 0`
- Empty continuation token
- Query text
- Query parameters

This bypasses the `recordQueries` flag by design, because query inspection is the method's purpose.

## Core Library Files

- `GalaxyCore.cs`: shared Cosmos container initialization, `recordQueries` flag handling, bulk option detection, serializer naming policy capture.
- `GalaxyBasic.cs`: create, bulk create, modify, bulk modify, remove, and direct id/partition-key reads.
- `Galaxy.cs`: advanced query execution for get/list/paged, generated query inspection, and query recommendations.
- `GalaxyProcedures.cs`: Cosmos stored procedure execute/create/read/replace/delete/list support.
- `UniverseOptions.cs`: optional persistent query-statistics storage configuration.
- `Response/Response.cs`: `Gravity` response record.
- `Exception/UniverseException.cs`: library-specific exception type.
- `global-usings.cs`: shared imports for the library.

## Builder Internals

### `UniverseBuilder.CreateQuery`

High-level phases:

1. `SanitizeInputs(...)`
2. Build selected columns.
3. Add aggregates and group columns when needed.
4. Add vector-distance score projections.
5. Build base `SELECT ... FROM c`.
6. Apply join shape when `ColumnOptions.Join` is present.
7. Validate clusters and catalyst rules.
8. Build grouped `WHERE` clauses.
9. Build scalar `ORDER BY` or rank ordering.
10. Build `GROUP BY`.
11. Create `QueryDefinition`.
12. Bind parameters.

Projection behavior:

- Default selection is `*`.
- `ColumnOptions.Names` replaces `*` with formatted properties.
- `Top > 0` prepends `TOP {Top}`.
- `IsDistinct` with selected columns prepends `DISTINCT`.
- Aggregates require `ColumnOptions.Names`.
- Aggregate aliases are generated from aggregate column names.
- Vector-distance catalysts append score expressions such as `VectorDistance(... ) AS {Column}Score`.

Join behavior:

- Base query becomes `SELECT ... FROM c JOIN {alias} IN c["arrayPath"]`.
- Join columns are formatted against the join alias.
- Join aggregate columns are formatted against the join alias.
- Join columns used with aggregates are added to group columns.

Where behavior:

- Each non-empty cluster becomes a parenthesized group.
- The cluster's `Where` value connects clusters.
- Inside a cluster, the first catalyst's `Where` value is ignored; subsequent catalyst `Where` values connect conditions.
- Scalar predicates use a first-emitted-condition flag, so repeated catalysts and leading rank catalysts preserve valid connectors without repeated collection copies or searches.
- Rank-only clusters (`VectorDistance` / `FTScore`) do not add a `WHERE` clause.
- `Defined` / `NotDefined` operators do not bind a value.

Ranking behavior:

- A single `VectorDistance` catalyst produces `ORDER BY VectorDistance(...)`.
- A single `FTScore` catalyst produces `ORDER BY RANK FullTextScore(...)`.
- Multiple rank catalysts produce `ORDER BY RANK RRF(...)`.
- Weighted RRF accepts one `Sorting.Direction.WEIGHTED` option containing numeric weights.
- Scalar sorting is rejected when rank catalysts are present.

Parameter binding:

- `Catalyst.ParameterName()` returns `p_` plus the lowercase SHA-256 hash of JSON `[Column, CatalystId]`. It accepts custom ids, distinguishes null from empty, and never inserts id text into SQL.
- A query-local map reuses bindings only when catalyst metadata matches and the value object is the same instance; distinct boxed/reference values get numeric suffixes when they share a base name. This preserves timestamp offsets and other values whose CLR equality differs from their serialized representation. All SQL rendering and parameter binding use this map.
- `FTScore` binds each term as `@{allocatedName}_ft{i}`. Aggregate/vector-score aliases are unchanged. Parameter names are opaque; consumers should inspect the actual names returned by `Gravity.Query.Parameters`.

### Identifier Formatting And Validation

Identifier handling has two pieces:

- `ValidateIdentifier(...)` validates the raw identifier before query text is built.
- `FormatProperty(alias, column)` validates the alias and path, then JSON-quotes every segment after applying the naming policy, escaping backslashes and policy-generated quotes.
- Raw grouping paths are kept separate from formatted SQL. The same formatted group list supplies wildcard projections and `GROUP BY`, retaining deduplication and root/join aliases without inferring formatting from bracket characters.

Validation currently rejects:

- Null/blank identifiers.
- Empty or whitespace dotted path segments, including leading/trailing dots.
- Semicolons.
- `--`
- `/*`
- `*/`
- Double quotes.
- Closing brackets.
- Length greater than 255.
- Control characters.

Nuance:

- Opening brackets are not independently rejected because a closing bracket is required to break out of bracket notation. The protection relies on rejecting closing brackets and double quotes.
- Dotted paths support nested JSON access; every segment must be nonblank. Explicit empty/whitespace catalyst aliases are rejected; null retains the implicit `c` default.
- Naming policy conversion is applied per path segment.

## Query Builder Map

`Builder/QueryBuilder.cs` is the most security-sensitive file in the repository.

It is responsible for:

- Validating all query identifiers before SQL text is built.
- Building `SELECT`, `JOIN`, `WHERE`, `ORDER BY`, `ORDER BY RANK`, and `GROUP BY`.
- Formatting property paths as Cosmos bracket paths, such as `c["metadata"]["sku"]`.
- Applying the configured JSON naming policy to query field names.
- Building parameterized `QueryDefinition` instances.
- Expanding `FTScore` string arrays into separate parameters.
- Appending vector-distance score aliases into the selected columns.
- Dispatching queries to the selected execution strategy.

Important constraints:

- Values must stay parameterized with `QueryDefinition.WithParameter`.
- Identifiers are not parameters in Cosmos SQL, so they must be validated and bracket-formatted.
- `ValidateIdentifier` rejects empty identifiers or path segments, SQL comment/statement separators, double quotes, closing brackets, excessive length, and control characters. Query construction caps clusters/catalysts/shaping fields at 1024, rank expressions at 64, full-text terms at 64 per expression and 1024 per query, each term at 4096 UTF-8 bytes, combined rank/full-text parameter bytes at 1 MiB, and generated SQL at 256 KiB. Projection nesting is limited to 32. Ordinary parameter objects remain unmodified and are not serialized to enforce these rank-specific budgets; see `docs/DECLARATIVE_QUERY_SYNTAX.md`.
- Weighted RRF sort values are validated as numeric comma-separated content before being inserted into SQL.
- `GenerateQuery` intentionally returns query text and parameters without executing the query.

## Query APIs

There are two equivalent query styles.

Declarative API:

- `Cluster`: a group of `Catalyst` filters joined by `AND` or `OR`.
- `Catalyst`: one filter condition containing column, value, where connector, operator, and alias.
- `ColumnOptions`: selected columns, distinct flag, top value, aggregates, and optional join.
- `Sorting.Option`: sorting or RRF weight options.
- `QueryHints`: strategy and request-option hints.

Fluent API:

- `Orbit<T>` is created with `galaxy.Query()`.
- `ClusterBuilder` exposes the operator helpers used inside `.Cluster(c => ...)`.
- `Select<TProjection>()` extracts public projection properties, excluding `[JsonIgnore]`.
- Without `Project`, `GetAsync()` applies only filters and selected columns and ignores `Top`, `Distinct`, `Aggregate`, `GroupBy`, sorting, joins, paging, and hints. With `Project`, it applies the full shape and rejects paging.
- `ToListAsync()` applies the full list query shape.
- `Paged()` and `WithHints()` are mutually exclusive in the fluent API.

Fluent API method behavior:

- `Cluster(Action<ClusterBuilder>)`: adds one cluster and consumes the pending cluster connector.
- `Or()` / `And()`: set the connector for the next cluster.
- `Select(params string[])`: adds explicit selected columns.
- `Select<TProjection>()`: adds public projection properties.
- `Distinct()`: sets distinct flag.
- `Top(int)`: rejects negative values.
- `OrderBy(...)`: adds sort option. The `alias` argument is stored on `Sorting.Option`, but current query building formats sort columns with alias `c`.
- `OrderByDescending(...)`: adds descending sort option.
- `WithWeights(...)`: adds weighted RRF option.
- `Aggregate(...)`: adds aggregate expression.
- `GroupBy(...)`: adds group columns.
- `Paged(...)`: stores page size and optional continuation token.
- `Join(...)`: stores a single join definition.
- `WithHints(...)`: stores query hints for non-paged list execution.
- `Build()`: produces clusters, nullable `ColumnOptions`, nullable sorting, and nullable groups.

## Supported Operators

`Q.Operator` supports:

- Equality and comparisons: `Eq`, `NotEq`, `Gt`, `Gte`, `Lt`, `Lte`
- Array/document checks: `In`, `NotIn`, `Contains`, `NotContains`, `Len`
- Pattern matching: `Like`, `NotLike`
- Defined checks: `Defined`, `NotDefined`
- Vector search: `VectorDistance`
- Full-text search: `FTContains`, `NotFTContains`, `FTContainsAll`, `NotFTContainsAll`, `FTContainsAny`, `NotFTContainsAny`, `FTScore`

`VectorDistance` and `FTScore` require `ColumnOptions.Top > 0`.

Operator SQL mapping:

- `Eq`: `{property} = @param`
- `NotEq`: `{property} != @param`
- `Gt`: `{property} > @param`
- `Gte`: `{property} >= @param`
- `Lt`: `{property} < @param`
- `Lte`: `{property} <= @param`
- `In`: `ARRAY_CONTAINS({property}, @param)`
- `NotIn`: `NOT ARRAY_CONTAINS({property}, @param)`
- `Contains`: `ARRAY_CONTAINS(@param, {property})`
- `NotContains`: `NOT ARRAY_CONTAINS(@param, {property})`
- `Len`: `ARRAY_LENGTH({property}) = @param`
- `Like`: `{property} LIKE @param`
- `NotLike`: `{property} NOT LIKE @param`
- `Defined`: `IS_DEFINED({property})`
- `NotDefined`: `NOT IS_DEFINED({property})`
- `FTContains`: `FullTextContains({property}, @param)`
- `NotFTContains`: `NOT FullTextContains({property}, @param)`
- `FTContainsAll`: `FullTextContainsAll({property}, @param)`
- `NotFTContainsAll`: `NOT FullTextContainsAll({property}, @param)`
- `FTContainsAny`: `FullTextContainsAny({property}, @param)`
- `NotFTContainsAny`: `NOT FullTextContainsAny({property}, @param)`
- `FTScore`: `FullTextScore({property}, @param...)` in rank ordering.
- `VectorDistance`: `VectorDistance({property}, @param)` in projection/ranking.

Catalyst rule validation:

- `Column` is required.
- `Defined` / `NotDefined` must not receive a value.
- `Like` / `NotLike` require a non-blank value containing `%`.
- `Contains` / `NotContains` require a non-string `IEnumerable`.
- `VectorDistance` requires a non-empty `float[]`, max length 4096, with no `NaN` or infinity values.
- `FTContains` / `NotFTContains` require a string.
- Full-text array operators and `FTScore` require a non-empty `string[]` with no blank terms.
- All other operators require a value.

## Execution Strategies

`Builder/Strategies/` contains the query execution pipeline.

- `DirectQueryStrategy`: default path for simple queries, optimistic direct execution enabled, CPU-core-limited concurrency by default.
- `GatewayQueryStrategy`: fallback path for complex queries, conservative concurrency and small continuation tokens.
- `VectorSearchStrategy`: preferred for vector and hybrid queries, optimistic direct execution disabled.
- `EnhancedContextStrategy`: wraps the chosen strategy with merged recommendation/user hints.
- `QueryStrategySelector`: chooses the highest-priority strategy that can handle the query, honoring `ForceStrategy` when valid.
- `QueryContext`: carries query type, max item count, hints, single-result mode, and ranking/grouping flags.

Query types and strategy eligibility use structured builder metadata rather than operator-name substrings in SQL. Include new operators in `QueryTypeDetector` when they affect routing. The remaining scalar complexity fallback retains the existing generated-SQL threshold of more than 20 space-separated tokens.

Strategy defaults:

- Direct: `MaxItemCount` defaults to `Q.Limits.MaxItems` (`1000`), `EnableOptimisticDirectExecution = true`, `MaxConcurrency = Environment.ProcessorCount`.
- Gateway: `MaxItemCount` defaults to `Q.Limits.MaxItems`, `EnableOptimisticDirectExecution = false`, `MaxConcurrency = 1`, `ResponseContinuationTokenLimitInKb = 1`.
- Vector: `EnableOptimisticDirectExecution = false`, `MaxConcurrency = Environment.ProcessorCount`; `MaxItemCount` is applied only when hints include it.

Single-result execution overrides page and buffer sizes to one in every strategy after hints are applied. List execution remains exhaustive, regardless of its page size.

Statistics recording:

- Strategy execution records both success and failure.
- Recorded failures include request charge accumulated before the exception and zero result count.
- All strategies return query text/parameters only if `recordQueries` is true.
- Strategy exceptions are rethrown after recording.

Recommendation behavior:

- `QueryStrategySelector` always asks `QueryTuner` for recommendations for the inferred `QueryType`.
- User hints override recommended hints.
- A forced strategy is used only when the named strategy exists and can handle the query.
- `GatewayQueryStrategy` can handle any query and is the fallback.

## Query Tuning And Statistics

`QueryTuner` records:

- Query hash
- Query type
- Request units
- Execution time
- Result count
- Success flag
- Timestamp
- Strategy used
- Hints used

The query hash is based on normalized query structure, not raw parameter values. Parameter names include generated catalyst ids, so `QueryTuner` normalizes `@\w+` to keep equivalent query shapes together.

Storage backends:

- `InMemoryStatisticsStorage`: default when no storage is configured.
- `FileStatisticsStorage`: JSON file persistence.
- `SqliteStatisticsStorage`: SQLite persistence with WAL mode, batched writes, and cleanup.

Path handling:

- Defaults are platform-aware.
- Azure App Service/Functions use local temp storage by default.
- Custom paths must never come from untrusted input.
- Unix-like systems attempt owner-only permissions on storage paths.

Rule-based recommendations:

- Vector, full-text, and hybrid searches suggest `MaxItemCount = 50` and `MaxBufferedItemCount = 50`.
- Aggregation suggests `MaxItemCount = 500` and `MaxBufferedItemCount = 1000`.
- Complex queries suggest `MaxConcurrency = 1` and `ResponseContinuationTokenLimitInKb = 1`.
- Simple and join queries get no rule-based hints.

Data-driven recommendations:

- Require at least 10 relevant samples in the last 24 hours.
- Hint configurations need at least 3 samples to be considered.
- Strategy recommendations need at least 5 samples.
- Hint recommendations prefer lower average RU, then higher success rate.
- Strategy recommendations prefer higher success rate, then lower average RU.

Storage behavior:

- `QueryTuner` persists through a bounded background queue; disposal waits up to 10 seconds for accepted writes. Tests inspecting persisted statistics must wait for that drain before asserting.
- `FileStatisticsStorage` writes at most the 1000 most recent entries.
- `FileStatisticsStorage` logs and returns empty data if the JSON file is corrupt.
- `SqliteStatisticsStorage` queues writes, flushes on batch size or timer, and retries failed flushes up to 3 times.
- SQLite reads drain all pending batches; batches are dequeued under the connection lock so reads cannot overtake an in-flight flush.
- `SqliteStatisticsStorage.Dispose()` stops and joins timer callbacks, drains admitted operations while their ordered flush barriers can still use the bounded writer, then completes and joins the writer before closing commands and connection.
- `PlatformDetection.IsAzureEnvironment()` checks `WEBSITE_INSTANCE_ID` and Functions environment variables.
- `ValidateStoragePath` normalizes with `Path.GetFullPath`, rejects null bytes, and requires a rooted result.

## Optional Document Cache

Document caching is an opt-in add-on configured through `UniverseOptions.WithDocumentCache(...)`. A default `UniverseOptions` instance leaves `DocumentCache` null, and repository read paths do not allocate cache entries, serialize query parameters, clone documents, or build cache keys unless the option is set.

Cached operations:

- `IGalaxyBasic<T>.Get(id, partitionKey...)`
- `IGalaxy<T>.Get(...)`
- `IGalaxy<T>.Get<TS>(...)`

Uncached operations:

- `List`
- `Paged`
- `GenerateQuery`
- Query recommendations
- Stored procedure operations

Invalidation behavior:

- Successful create/modify/remove operations clear same-repository single-document query cache entries.
- Successful create/modify operations update known point-read entries.
- Successful remove operations delete known point-read entries.
- Legacy bulk create/modify reconcile each committed partition before reporting any sibling failure. Indeterminate batch transport failures evict submitted point keys and invalidate query entries; successful newer bulk/atomic operations use the same cache-aware execution path.
- Read misses capture a mutation generation before Cosmos I/O; cache insertion verifies it atomically, so a delayed read cannot undo an intervening write invalidation. A fixed 256-stripe generation array bounds metadata; collisions conservatively suppress unrelated fills.
- External writers are not observed; they rely on cache time-to-live expiration.

Cache keys are SHA-256 hashes built from an opaque Cosmos client namespace, operation kind, database, container, source entity type, result type, and operation inputs. A cache-owned `ConditionalWeakTable<CosmosClient, string>` assigns namespaces by exact client-instance identity without retaining credentials or preventing client collection. Point keys, query keys, and invalidation scopes all include that namespace. Repositories sharing the same client and cache options can share matching entries; different client instances are isolated even at the same endpoint. A client must represent one stable authorization context; recreate it or disable caching when its authority changes.

Raw ids, partition-key values, query text, and parameter values are not retained as readable dictionary keys. Cached values are cloned by default with the owning client's custom `CosmosSerializer`, or with `System.Text.Json` settings aligned with `UniverseSerializer` when the client has no custom serializer. Submitted create and batch models are normalized through that codec before cache insertion, including when ordinary read cloning is disabled. Clone failures skip or evict the affected cache entry. Writes through another client namespace rely on TTL expiry for visibility.

Query parameter signatures are restricted to null, string, boolean, and built-in numeric values and include runtime type identity. Object and collection parameters, including anonymous-object partial-match authorization filters, bypass query caching because the cache cannot guarantee equivalence with Cosmos parameter serialization. This prevents distinct filters from collapsing into the same key when document-cloning options omit getter-only properties, nulls, or serializer-specific members. Point reads retain their existing cache behavior.

The cache holds one linked-list node per dictionary entry under a short structural lock, so `MaxEntries` bounds eviction metadata as well as documents. Serialized UTF-8 budgets default to 1 MiB per entry and 32 MiB total; oversized entries are skipped before cloning, and FIFO eviction enforces count and total bytes together. These budgets measure serialized size rather than exact managed heap usage. The existing three-argument options constructor and `WithDocumentCache` remain available; five-argument overloads expose custom budgets. Overwrites refresh TTL/value/ETag in their original FIFO position; remove/reinsert joins the tail. Reads take an entry snapshot under the lock and clone outside it. A clone failure removes only that exact snapshot, never a concurrent replacement. Expiration, explicit removal, and query invalidation remove their list nodes too.

The query tuner uses the same quote-aware normalizer while retaining the existing `@p` substitution for ordinary query hashes. Existing JSON/SQLite statistics and their schemas are retained; only hashes for previously misnormalized quoted text can change.

## Data Model And Serialization

`ICosmicEntity` requires:

- `id`
- `AddedOn`
- `ModifiedOn`
- `CountAggregate`

`CosmicEntity` initializes `id` with a version 7 GUID and sets date fields in UTC.

`UniverseSerializer` wraps `System.Text.Json` through `JsonObjectSerializer` and defaults to:

- Optional naming policy
- Case-insensitive property names
- Ignore null values when writing
- Skip unmapped members
- Ignore read-only fields and properties

If a serializer naming policy is configured on `CosmosClientOptions`, query field names are converted with the same policy when SQL is generated.

## Partition Keys

Partition key conventions live in `Attributes/PartitionKeyAttribute.cs` and `Extensions/PartitionKeyExtension.cs`.

Rules:

- Entity types must inherit from `CosmicEntity` for `typeof(T).BuildPartitionKey()`.
- One to three partition-key properties are supported.
- Duplicate sequence values are rejected.
- Partition keys are ordered by sequence.
- The default partition-key path name is `property.Name.ToLowerCamelCase()`.
- `PartitionKeyAttribute` can override the path name with `keyName`.
- Runtime partition-key values cannot be null.

Partition-key methods:

- `Type.BuildPartitionKey()` returns path strings such as `/tenantId`.
- `ICosmicEntity.BuildPartitionKey()` returns a Cosmos `PartitionKey` for the entity instance.
- `ICosmicEntity.PartitionKeys()` returns ordered string values, useful for APIs that accept `params string[]`.

Nuances:

- `Type.BuildPartitionKey()` requires the type to inherit from `CosmicEntity`, not merely implement `ICosmicEntity`.
- Runtime value extraction works from `ICosmicEntity` and reflects on the concrete entity type.
- Default path names use `ToLowerCamelCase()`, so serialized naming policy and partition-key path names can diverge if a custom serializer naming policy is used without explicit `keyName`.

## Stored Procedures

`GalaxyProcedure` exposes stored procedure operations directly through Cosmos DB SDK script APIs.

Security nuance:

- `CreateSProc` and `ReplaceSProc` accept stored procedure bodies as raw JavaScript strings.
- The library checks execution parameters for JSON serializability, but it does not sandbox or validate stored procedure body content.
- Treat stored procedure body inputs as trusted administrative inputs only.

## Security Posture

The primary security guarantee is query injection protection.

Current protections:

- Query values are parameterized.
- Identifiers are validated before SQL construction.
- Identifier segments are rendered as bracketed Cosmos property access.
- FTScore values are parameterized even in rank ordering.
- RRF weights are validated as numeric content.
- Optional document-cache dictionary keys are hashed instead of storing raw ids, partition keys, query text, or parameter values as readable key strings.
- Query statistics store query hashes and performance metadata, not raw parameter values.
- `recordQueries` defaults to `false`.

Important risks and caveats:

- `recordQueries: true` includes full query text and parameter values in `Gravity`.
- `GenerateQuery` always returns full query text and parameters.
- README and examples print query parameters for debugging; do not copy that pattern into production logging.
- Persistent statistics paths can create files/directories; never derive those paths from user input.
- Stored procedure bodies are raw code strings and should remain privileged/admin-only inputs.

Security-sensitive review checklist:

- Any new string inserted into SQL text must either be enum-derived, validated as an identifier, or strictly parsed to a safe primitive form.
- Any new user value must be bound through `QueryDefinition.WithParameter`.
- Any new operator that expands multiple values should follow the `FTScore` pattern of separate parameter names.
- Any new identifier field in an option record should be added to `SanitizeInputs`.
- Any new debug/log output should be checked for query parameters, continuation tokens, Cosmos keys, and stored procedure bodies.
- Any new file path option should use `PlatformDetection.ValidateStoragePath` and document that it must not be user-controlled.
- Any new persistent local file should set restrictive permissions where possible.
- Any new stored procedure capability should assume body content is trusted admin code, not user input.

## Examples Map

`DarkMatter` is the runnable example project.

- `Program.cs`: wires Cosmos client, repositories, and sequential example execution.
- `Repository/MyRepo.cs`: sample repository using file persistence and `recordQueries: true`.
- `Repository/MyRepoVector.cs`: sample vector repository.
- `Models/`: sample document models.
- `Helpers/`: test/vector data generation.
- `Examples/Example1_BasicPagedQuery.cs`: paging.
- `Examples/Example2_AggregatesWithGroupBy.cs`: aggregates.
- `Examples/Example3_TopAndDistinct.cs`: top and distinct.
- `Examples/Example4_ComplexFiltering.cs`: complex filters.
- `Examples/Example5_SingleItemOperations.cs`: create/get/modify/remove.
- `Examples/Example6_BulkOperations.cs`: bulk create/modify.
- `Examples/Example7_AdvancedAggregation.cs`: aggregation model projection.
- `Examples/Example8_SalesAnalysis.cs`: date/range examples.
- `Examples/Example9_VectorSearch.cs`: vector search.
- `Examples/Example10_FullTextSearch.cs`: full-text search.
- `Examples/Example11_HybridVectorFullText.cs`: hybrid vector/full-text RRF.
- `Examples/Example12_QueryGeneration.cs`: generated SQL without execution.
- `Examples/Example13_QueryOptimization.cs`: strategies, hints, storage examples.
- `Examples/Example14_SQLInjectionProtection.cs`: injection protection demonstration.
- `Examples/Example15_ContainsOperator.cs`: contains/not-contains.
- `Examples/Example16_ProjectionSelect.cs`: projection select.

Example sequencing:

`Program.cs` currently runs the example set sequentially and sums RU. This gives the examples a rough integration-test shape, but it is not deterministic and depends on live Cosmos state, feature availability, and seeded sample data.

## Tests Map

`Universe.Tests` currently focuses on local, deterministic behavior.

- `Builder/OrbitQueryTests.cs`: fluent API mirrors declarative API.
- `Builder/NamingPolicyQueryTests.cs`: naming policy effects on SQL columns and aliases.
- `Builder/ProjectionSelectTests.cs`: type-based projection column extraction.
- `Builder/QueryTypeDetectorTests.cs`: structured query classification and strategy selection, including operator-named properties and parameters.
- `Builder/QueryExecutionTests.cs`: bounded single-result reads across all strategies, empty/error pages, disposal, RU/statistics, hints, and repository SQL/cache behavior.
- `Builder/ScalarFilterTests.cs`: scalar predicate connectors, mixed ranking, repeated catalysts, and deterministic linear collection traversal.
- `Extensions/PartitionKeyExtensionTests.cs`: partition-key path and runtime value ordering, explicit key names, duplicate sequence validation, and max-level validation.
- `Storage/FileStatisticsStorageTests.cs`: JSON statistics storage behavior.
- `Storage/InMemoryStatisticsStorageTests.cs`: in-memory storage behavior.
- `Storage/PlatformDetectionTests.cs`: path/platform detection behavior.
- `Storage/SqliteStatisticsStorageTests.cs`: SQLite persistence behavior.
- `Caching/DocumentCacheTests.cs`: cache key hashing, TTL, eviction, cloning, and query-key normalization behavior.
- `Caching/DocumentCacheRepositoryTests.cs`: opt-in repository cache behavior using fake Cosmos SDK abstractions.
- `Batch/BatchExecutionTests.cs`: in-memory atomic/bulk execution, rollback, ETags, chunking, concurrency, partial success, cancellation, and cache behavior.
- `Batch/LegacyInterfaceCompatibilityTests.cs`: default-interface compatibility for implementations built around the pre-batch `IGalaxyBasic<T>` contract.
- `Tuner/QueryTunerTests.cs`: recommendation and persistence behavior.
- `Tuner/QueryTunerPerformanceTests.cs`: tuner performance checks.
- `Helpers/TestStatisticsFactory.cs`: shared test statistics builders.
- `Helpers/QueryTestContainer.cs`: scripted Cosmos read/query responses shared by cache and execution tests, with request capture and iterator lifecycle counters.

When adding tests:

- Query SQL tests should normalize generated parameter names unless the parameter naming itself is under test.
- Prefer unit-level query-generation tests over live Cosmos tests for query builder changes.
- Add malicious input cases when touching `QueryBuilder`, option records, operators, sorting, joins, groups, projections, or aliases.
- Storage tests that create files should use temporary or unique paths and clean up SQLite sidecar files (`-wal`, `-shm`) too.
- If a performance test is added, mark it so CI's `Category!=Performance` filter behaves intentionally.

## Documentation Map

- `README.md`: public package setup and API overview.
- `docs/FLUENT_QUERY_BUILDER.md`: fluent `Orbit` API reference.
- `docs/DECLARATIVE_QUERY_SYNTAX.md`: `Cluster` / `Catalyst` API reference.
- `docs/VECTORDISTANCE_USAGE.md`: vector search guide.
- `docs/FULLTEXT_USAGE.md`: full-text search guide.
- `docs/QUERY_EXECUTION_STRATEGIES.md`: strategy and tuning overview.
- `docs/ROADMAP_STATUS.md`: source-verified feature roadmap status and remaining gaps.
- `docs/SQLITE_STATISTICS_STORAGE.md`: SQLite persistence configuration.
- `docs/ADAPTIVE_QUERY_OPTIMIZATION_DESIGN.md`: design notes for adaptive optimization.

Documentation consistency:

- Public README examples should not imply `recordQueries: true` is a production default.
- Query examples should stay aligned with generated SQL behavior in tests.
- Feature docs should mention Cosmos DB feature prerequisites when relevant, especially vector and full-text search.
- If `UniverseQuery.csproj` package metadata changes, verify README installation/version references.

## Development Conventions

- Keep public API changes deliberate; this is a NuGet package.
- Prefer existing domain names and astronomy terminology.
- Keep query generation changes covered by unit tests.
- Treat `QueryBuilder.cs` as security-sensitive.
- Do not convert parameterized values into interpolated SQL.
- If adding identifier-bearing options, route them through `ValidateIdentifier` before formatting.
- If adding raw SQL-like syntax, add explicit tests for injection attempts.
- If changing naming policy behavior, update naming policy and projection tests.
- If changing storage behavior, include file and SQLite tests as applicable.
- Use `UniverseException` for library validation errors.
- Cosmos SDK exceptions are often allowed through or wrapped only with status-level context; preserve that pattern unless intentionally changing error behavior.
- `dotnet format` can touch broad files; prefer scoped formatting when possible.

## Git, CI, And Review

Repository workflow from existing instructions:

- Main branch: `dev`
- Production branch: `prod`
- Current working branch may vary; inspect with `git branch --show-current`.
- GitHub Actions build the .NET solution on push.
- NuGet publishing is handled by release workflow.
- Semgrep scans run on PRs and commits.
- Aikido workflow findings are remediated by disabling persisted Git credentials on the affected checkout steps.
- After every commit or before pushing, run local CodeRabbit review when available:

```bash
coderabbit review
```

## Common Gotchas

- `recordQueries` is convenient but can expose sensitive parameter values.
- Document caching is disabled by default; enabling it creates process-local state and external writers are visible after TTL expiration, not through distributed invalidation.
- `DarkMatter` examples intentionally use debug-style logging and should not define production logging standards.
- `Paged()` stops after the first non-empty page and rejects grouped queries before execution; use `List()` for grouped results.
- Query hints are not accepted with fluent paged queries.
- `GetAsync()` without `Project` does not apply most list-shaping options.
- `ColumnOptions.Top` is required for rank/vector operators.
- Cosmos DB column names are case-sensitive.
- `PartitionKey` attribute sequence matters for multi-level partition keys.
- File/SQLite persistence defaults differ on Azure versus local runtime.
- Stored procedure methods are administrative power tools, not user-facing script execution endpoints.
