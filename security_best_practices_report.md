# Security Best Practices Report

Date: 2026-05-22

Scope: static review of the `UniverseQuery` library, example harness, documentation, and GitHub workflow configuration for blue-team assessment.

## Executive Summary

The codebase has a strong baseline for Cosmos SQL value parameterization: query values are bound through `QueryDefinition.WithParameter`, and many identifier inputs are validated before SQL text is constructed. The highest-risk issue is that the identifier validator is used for both bracketed property paths and raw SQL identifier positions, but it does not enforce a strict SQL-identifier shape. That leaves raw alias and generated `AS` alias positions able to receive whitespace and punctuation that can corrupt generated SQL and may undermine the library's stated injection-protection guarantee when column/alias names are derived from untrusted request input.

No critical findings were identified. Semgrep was run locally with `semgrep scan . --config auto --quiet` and completed without console findings. Manual review found one high-severity issue, three medium-severity issues, and four low-severity hardening items.

## Critical Findings

None identified.

## High Severity

### H-001: Identifier validation is too permissive for raw SQL alias positions

Impact: If a consumer passes user-controlled field, aggregate, vector, or alias names into the query builder, crafted identifier text can be inserted into raw SQL alias positions and corrupt or potentially alter the generated Cosmos SQL.

Evidence:

- `SanitizeInputs` validates aggregate columns, join aliases, join columns, catalyst columns, catalyst aliases, sorting columns, and groups through the same `ValidateIdentifier` helper: `code/Universe/Builder/QueryBuilder.cs:348-416`.
- `ValidateIdentifier` rejects blank values, semicolons, comments, double quotes, closing brackets, excessive length, and control characters, but it does not reject spaces, parentheses, commas, opening brackets, arithmetic symbols, or SQL keywords: `code/Universe/Builder/QueryBuilder.cs:419-453`.
- Aggregate aliases use `ConvertName(aggregate.Column)` directly in raw `AS` alias positions: `code/Universe/Builder/QueryBuilder.cs:74-78` and `code/Universe/Builder/QueryBuilder.cs:153-157`.
- Vector score aliases use `ConvertName(catalyst.Column)` directly in raw `AS` alias positions: `code/Universe/Builder/QueryBuilder.cs:101-112`.
- Join aliases are inserted raw in the `JOIN {join.Alias} IN ...` clause: `code/Universe/Builder/QueryBuilder.cs:127-141` and `code/Universe/Builder/QueryBuilder.cs:170-172`.
- Catalyst aliases are prefixed raw in `FormatProperty(alias, column)`: `code/Universe/Builder/QueryBuilder.cs:498-536`.

Why it matters:

The current validation is mostly sufficient for property path segments because `FormatProperty` wraps segments as bracketed JSON property access. It is not sufficient for places where the validated value is used as SQL syntax, such as aliases after `AS`, join aliases, and property aliases. A column such as `price from c` is safe as a bracketed path segment but not safe as `AS price from c_Sum`.

Recommended remediation:

- Split validation into separate helpers:
  - Property path validation for values rendered by `FormatProperty`.
  - SQL alias validation for raw alias positions.
- Require raw aliases to match a strict pattern such as `^[A-Za-z_][A-Za-z0-9_]*$`.
- For generated aggregate/vector aliases, derive safe aliases from sanitized tokens rather than raw column names. For nested columns, replace invalid characters with `_`.
- Add tests for aggregate aliases, vector score aliases, join aliases, and catalyst aliases using whitespace, parentheses, commas, keywords, and bracket fragments.
- Update `Example14_SQLInjectionProtection.cs` to include raw-alias and aggregate-alias attack cases.

## Medium Severity

### M-001: Query shape controls are not consistently bounded, enabling RU/cost denial-of-service when exposed to callers

Impact: If API consumers can control `Top`, page size, or query hints, they can request large result sets, high buffering, or high concurrency and drive unnecessary RU consumption, memory pressure, and service cost.

Evidence:

- `Orbit.Top(int count)` rejects only negative values, with no upper bound: `code/Universe/Builder/Orbit.cs:78-85`.
- `ColumnOptions.Top` is inserted directly into SQL when greater than zero: `code/Universe/Builder/QueryBuilder.cs:48-49`.
- `Orbit.Paged(int size, ...)` stores the supplied page size without validation: `code/Universe/Builder/Orbit.cs:125-129`.
- Paged queries pass `page.Size` directly into `QueryRequestOptions.MaxItemCount`: `code/Universe/Galaxy.cs:88-90`.
- `QueryHints` accepts `MaxItemCount`, `MaxBufferedItemCount`, `MaxConcurrency`, and `ResponseContinuationTokenLimitInKb` without range validation: `code/Universe/Builder/Options/QueryHints.cs:38-67`.
- Direct, gateway, and vector strategies apply hint values directly after conversion: `code/Universe/Builder/Strategies/DirectQueryStrategy.cs:52-62`, `code/Universe/Builder/Strategies/GatewayQueryStrategy.cs:44-51`, and `code/Universe/Builder/Strategies/VectorSearchStrategy.cs:48-58`.

Recommended remediation:

- Enforce positive page size and a maximum page size.
- Enforce `Top` limits, using `Q.Limits.MaxItems` and `Q.Limits.MaxVectorItems` where appropriate.
- Clamp or reject unsafe hint values for max item count, buffered count, concurrency, and continuation token size.
- Document that query-shaping values must not be passed through directly from public request parameters without authorization and server-side limits.

### M-002: Repository construction requires database/container create permissions at runtime

Impact: Runtime identities must have create-database/create-container capability, weakening least privilege and increasing the impact of a compromised service credential.

Evidence:

- `GalaxyCore` calls `CreateDatabaseIfNotExistsAsync(database).GetAwaiter().GetResult()` during repository construction: `code/Universe/GalaxyCore.cs:31`.
- It then calls `CreateContainerIfNotExistsAsync(containerProps).GetAwaiter().GetResult()` during the same construction path: `code/Universe/GalaxyCore.cs:33-42`.

Recommended remediation:

- Add an option to disable auto-provisioning in application runtime.
- Move database/container creation to deployment, migration, or bootstrap tooling.
- Document the minimum Cosmos RBAC/data-plane permissions needed for runtime when auto-provisioning is disabled.
- Prefer read/write-only runtime identities for production repositories.

### M-003: Stored procedure create/replace APIs accept raw executable body text

Impact: If these APIs are exposed beyond a trusted administrative path, callers can create or replace executable Cosmos stored procedure code.

Evidence:

- `CreateSProc` assigns caller-supplied `body` to `StoredProcedureProperties.Body`: `code/Universe/GalaxyProcedures.cs:49-58`.
- `ReplaceSProc` assigns caller-supplied `newBody` to `StoredProcedureProperties.Body`: `code/Universe/GalaxyProcedures.cs:88-98`.
- `ExecSProc` validates that parameters are serializable, but it does not constrain what stored procedure code can do once installed: `code/Universe/GalaxyProcedures.cs:16-37`.

Recommended remediation:

- Treat `IGalaxyProcedure` as an admin-only interface.
- Do not expose `CreateSProc` or `ReplaceSProc` through user-facing routes.
- Consider explicit documentation warnings on `IGalaxyProcedure`.
- If stored procedures are managed by the app, load approved bodies from version-controlled resources rather than request payloads.

## Low Severity

### L-001: Debug query surfaces can disclose sensitive filter values

Impact: Query text, parameters, and continuation tokens can leak PII or sensitive application data if returned to clients or written to logs.

Evidence:

- `GalaxyCore` documents that `recordQueries` includes full query text and parameter values in `Gravity`: `code/Universe/GalaxyCore.cs:20-21`.
- Strategy responses include query text and parameters when `recordQueries` is enabled: `code/Universe/Builder/Strategies/DirectQueryStrategy.cs:90-94`, `code/Universe/Builder/Strategies/GatewayQueryStrategy.cs:79-83`, and `code/Universe/Builder/Strategies/VectorSearchStrategy.cs:86-90`.
- `GenerateQuery` always returns query text and parameters: `code/Universe/Galaxy.cs:112-115`.
- README examples show printing query text and parameters: `README.md:92-102`.

Recommended remediation:

- Keep `recordQueries` disabled in production.
- Add a redacted query-inspection mode for production diagnostics.
- Make README/debug examples explicitly label parameter printing as development-only.
- Avoid returning `Gravity.Query` from public API responses.

### L-002: Custom statistics storage paths can write wherever the process has permission

Impact: If a custom statistics path is derived from untrusted input, the library can create directories and overwrite files within process permissions.

Evidence:

- `UniverseOptions.WithFilePersistence` and `WithSqlitePersistence` accept caller-provided paths: `code/Universe/UniverseOptions.cs:23-49`.
- `ValidateStoragePath` normalizes paths and rejects null bytes but does not enforce an application-owned base directory: `code/Universe/Builder/Strategies/Storage/PlatformDetection.cs:53-67`.
- `FileStatisticsStorage` creates directories and writes the JSON statistics file at the resolved path: `code/Universe/Builder/Strategies/Storage/FileStatisticsStorage.cs:27-38` and `code/Universe/Builder/Strategies/Storage/FileStatisticsStorage.cs:62-78`.
- `SqliteStatisticsStorage` opens a SQLite database at the resolved path with `ReadWriteCreate`: `code/Universe/Builder/Strategies/Storage/SqliteStatisticsStorage.cs:51-82`.

Recommended remediation:

- Keep custom paths configuration-only and never user-controlled.
- Consider enforcing an optional base directory allowlist.
- Add extension validation for `.json` and `.db` paths if practical.
- Keep the existing restrictive Unix file permissions.

### L-003: Example project normalizes hardcoded credential placeholders

Impact: Although no real secret is present, the example shape encourages editing credentials directly into source files for local execution.

Evidence:

- `DarkMatter/Program.cs` declares `CosmosDbUri` and `CosmosDbPrimaryKey` as string variables initialized with placeholders: `code/DarkMatter/Program.cs:17-24`.
- README production setup uses environment variables for Cosmos URI/key, which is the safer pattern: `README.md:41-46`.

Recommended remediation:

- Update `DarkMatter` to read credentials from environment variables or user secrets.
- Fail fast with a clear message when values are missing.
- Keep placeholders out of source-code assignment examples where possible.

### L-004: Dependency and ownership automation appears stale for the active repo layout

Impact: Dependency update coverage and ownership routing may miss the active projects, reducing blue-team visibility into vulnerable package updates and review ownership.

Evidence:

- Dependabot NuGet updates point at `./functions/`, but the active projects are under `code/Universe`, `code/Universe.Tests`, and `code/DarkMatter`: `.github/dependabot.yml:6-11`.
- CODEOWNERS references `/functions/`, `/app_portal/`, `/app_store/`, and `/web_admin/`, which do not match the current source tree: `.github/CODEOWNERS:11-17`.
- CI does restore/build/test the active `./code/` solution path: `.github/workflows/dotnet-ci.yml:16-18`, `.github/workflows/dotnet-ci.yml:29-31`, and `.github/workflows/dotnet-ci.yml:42-44`.

Recommended remediation:

- Point Dependabot NuGet updates at `/code/Universe`, `/code/Universe.Tests`, and `/code/DarkMatter` or the most appropriate solution/project roots.
- Update CODEOWNERS to cover `/code/Universe/`, `/code/Universe.Tests/`, `/code/DarkMatter/`, `/docs/`, and `.github/`.
- Keep Semgrep PR and push workflows enabled; they are currently present at `.github/workflows/semgrep--pr-ci.yml:1-20` and `.github/workflows/semgrep-ci.yml:1-24`.

## Positive Controls Observed

- Query values are parameterized through `QueryDefinition.WithParameter`.
- `recordQueries` defaults to false.
- `GalaxyCore` already warns that recorded queries can expose sensitive parameter values.
- File and SQLite statistics storage document that custom paths must not be untrusted.
- Statistics persistence stores query hashes and performance data, not raw query text or parameter values.
- Vector values reject null, empty arrays, over-large dimensions, `NaN`, and infinity.
- RRF weight values are parsed as numeric comma-separated content before being inserted into SQL.
- CI includes restore, build, tests, and Semgrep workflows.

## Suggested Remediation Order

1. Fix H-001 by adding strict raw-alias validation and tests for aggregate/vector/join/catalyst alias injection cases.
2. Add server-side bounds for `Top`, page size, and query hints.
3. Add an opt-out or separate provisioning mode for database/container creation.
4. Update Dependabot and CODEOWNERS to match the active repository layout.
5. Harden examples and documentation around query logging and Cosmos credentials.
