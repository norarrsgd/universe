# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Universe** (aka `UniverseQuery`) is a C# library that simplifies querying Azure Cosmos DB by providing a fluent, type-safe query builder. It wraps the Cosmos DB SDK with an intuitive abstraction layer that reduces boilerplate while maintaining performance and flexibility.

**Key Concepts:**
- **Galaxy**: Base repository class for querying Cosmos DB containers
- **CosmicEntity**: Base class/interface for Cosmos DB documents with standard fields (`id`, `AddedOn`, `ModifiedOn`)
- **Catalyst**: Represents a single WHERE clause condition (column, value, operator)
- **Cluster**: Groups multiple Catalysts together with AND/OR logic
- **Gravity**: Response object containing RU consumption, continuation tokens, and query debug info
- **Query Builder**: Internal system that constructs parameterized SQL queries with SQL injection protection

## Solution Structure

```
code/
├── UniverseQuery.sln          # Main solution file
├── Universe/                  # Main library (UniverseQuery package)
│   ├── Galaxy.cs              # Advanced query operations (List, Paged, with Clusters)
│   ├── GalaxyBasic.cs         # Basic CRUD operations (Get by ID, Create, Modify, Remove)
│   ├── GalaxyCore.cs          # Core Container/Client initialization
│   ├── GalaxyProcedures.cs    # Stored procedure operations
│   ├── Builder/               # Query construction
│   │   ├── QueryBuilder.cs    # Main query builder with SQL injection protection
│   │   ├── Options/           # Query options (Catalyst, Cluster, ColumnOptions, QueryHints, etc.)
│   │   └── Strategies/        # Query execution strategies and optimization
│   │       ├── QueryTuner.cs             # Query optimization recommendations
│   │       ├── QueryStrategySelector.cs  # Selects optimal execution strategy
│   │       ├── DirectQueryStrategy.cs    # Standard query execution
│   │       ├── GatewayQueryStrategy.cs   # For cross-partition queries
│   │       ├── VectorSearchStrategy.cs   # For vector similarity queries
│   │       └── Storage/                  # Query statistics persistence
│   ├── Interfaces/            # Public interfaces (IGalaxy, IGalaxyBasic, ICosmicEntity, IGalaxyProcedure)
│   ├── Extensions/            # Helper extensions (PartitionKey building, etc.)
│   ├── Response/              # Gravity response object
│   └── Exception/             # UniverseException
└── DarkMatter/                # Example/test project
    ├── Examples/              # 14 example files demonstrating all features
    ├── Models/                # Sample Cosmos entity models
    ├── Repository/            # Sample repository implementations
    └── Program.cs             # Runs all examples sequentially
```

## Development Commands

### Build
```bash
cd code
dotnet restore
dotnet build
```

### Run Examples
The DarkMatter project contains 14 example files demonstrating all library features:
```bash
cd code/DarkMatter
# Edit Program.cs to add your Cosmos DB connection string first
dotnet run
```

### Build for Release
```bash
cd code
dotnet build -c Release
```

### Pack NuGet Package
```bash
cd code/Universe
dotnet pack -c Release
```

## Architecture Highlights

### Query Building System
- **QueryBuilder** (`Builder/QueryBuilder.cs`): Core query construction with automatic parameterization to prevent SQL injection
- **Input Sanitization**: Column names, aliases, and identifiers are validated against strict patterns to prevent injection
- **Parameterized Queries**: All user values are passed as parameters, never interpolated into SQL strings
- **Catalyst Validation**: The `Catalyst` struct validates rules before query construction (required fields, operator compatibility, etc.)

### Query Execution Strategies (Planned Enhancement)
The library uses a strategy pattern for query execution:
- **DirectQueryStrategy**: Standard queries with known partition keys
- **GatewayQueryStrategy**: Cross-partition queries requiring gateway mode
- **VectorSearchStrategy**: Optimized for vector similarity search (VECTOR_DISTANCE)
- **EnhancedContextStrategy**: For queries with extensive query hints

Strategy selection is automatic based on query characteristics (see `QueryStrategySelector.cs`).

### Query Optimization (v3.2.0+)
- **QueryTuner**: Tracks query execution statistics (RU consumption, execution time, success rate)
- **Adaptive Recommendations**: Uses historical data to suggest optimal QueryHints
- **Query Types**: 7 distinct types (Simple, CrossPartition, VectorSearch, Aggregation, FullText, Hybrid, Complex)
- **Statistics Persistence**: Optional file-based storage for learning across sessions

Currently (v3.1.x), optimization is rule-based. Full adaptive learning is planned for v3.2.0 (see `docs/ADAPTIVE_QUERY_OPTIMIZATION_DESIGN.md`).

### Special Features
- **Vector Search**: Support for Azure Cosmos DB's VECTOR_DISTANCE function
- **Full-Text Search**: Multiple FT operators (FTContains, FTContainsAll, FTContainsAny, FTScore, etc.)
- **Aggregations**: Built-in support for COUNT, SUM, MIN, MAX, AVG with GROUP BY
- **Pagination**: Continuation token-based paging via `Paged()` method
- **Bulk Operations**: Optimized bulk create/modify using CosmosClient's AllowBulkExecution
- **Stored Procedures**: Full lifecycle management (Create, Read, Replace, Delete, Execute)

### PartitionKey Handling
- Use `[PartitionKey]` attribute on properties to mark partition key fields
- Multiple partition keys supported via `[PartitionKey(1)]`, `[PartitionKey(2)]`, etc.
- `typeof(MyModel).BuildPartitionKey()` extension generates the partition key list

## Important Conventions

### Naming
- **Galaxy**: Represents a repository for a single Cosmos DB container
- **Cosmic/Universe**: All domain terms use space/astronomy metaphors
- Use `recordQueries: true` in Galaxy constructor to enable query debug logging (adds Query details to Gravity response)

### Error Handling
- Library throws `UniverseException` for validation errors (bad Catalyst configuration, etc.)
- Cosmos DB SDK exceptions (`CosmosException`) are not wrapped, allowing direct access to status codes and RU info

### Serialization
- Library provides `UniverseSerializer` with sensible defaults (preserves property names, case-insensitive, skips nulls)
- Uses System.Text.Json (not Newtonsoft.Json) for serialization
- Newtonsoft.Json is a dependency but only for internal compatibility

### Security
- SQL injection protection is built-in via parameterized queries
- Column names and identifiers are validated with regex patterns
- See `Example14_SQLInjectionProtection.cs` for detailed examples

## Target Framework
- **Current**: .NET 10.0 (C# 14.0)
- **Package**: `Microsoft.Azure.Cosmos` v3.56.0

## Git Workflow
- **Main branch**: `dev` (for PRs)
- **Production branch**: `prod`
- **Current branch**: `beta`

## CI/CD
- **GitHub Actions**: `.github/workflows/dotnet-ci.yml` runs restore and build on every push
- **NuGet Publishing**: `.github/workflows/nuget-publish.yml` for package releases
- **Security**: Semgrep scans on PRs and commits

## Documentation
- `README.md`: Comprehensive usage examples and API reference
- `docs/VECTORDISTANCE_USAGE.md`: Vector search feature guide
- `docs/FULLTEXT_USAGE.md`: Full-text search feature guide
- `docs/ADAPTIVE_QUERY_OPTIMIZATION_DESIGN.md`: Query optimization design (planned features)
- `docs/QUERY_EXECUTION_STRATEGIES.md`: Strategy pattern documentation
- `code/DarkMatter/Examples/`: 14 runnable examples covering all features

## Testing
This project uses the DarkMatter example project as a test harness. There are no unit tests; examples are run against a live Cosmos DB instance.
