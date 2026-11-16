using Microsoft.Azure.Cosmos;
using Universe;
using Universe.Builder.Options;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

/// <summary>Generates a structured query using synthetic values without contacting Cosmos.</summary>
public static class Example18_QueryCapabilities
{
    public static void Run()
    {
        using CosmosClient client = new("https://localhost:8081", Convert.ToBase64String(new byte[64]));
        using OfflineGalaxy repository = new(client);
        IGalaxy<ExampleEntity> galaxy = repository;
        Gravity generated = galaxy.Query()
            .Cluster(c => c.StartsWith("pk", "tenant:")
                .StringContains("data.name", "sample", ignoreCase: true)
                .StringEquals("data.owner", "user", ignoreCase: true)
                .IsString("data.name").IsNull("data.deletedAt")
                .ArrayContains("data.shares", new { key = "user" }, partialMatch: true)
                .Compare(QueryOperand.ArrayLength("data.shareEvents"), Q.Operator.Gt, QueryOperand.Field("data.auditQueued")))
            .Project(p => p.Field("id", "id").Object("data", data => data.Field("name", "data.name").Field("owner", "data.owner")))
            .WithConsistency(ConsistencyLevel.Strong)
            .GenerateQuery();
        Console.WriteLine(generated.Query.Text);
        Console.WriteLine($"Bound parameters: {generated.Query.Parameters.Count}; RU: {generated.RU}");
        Console.WriteLine("SQL generation only: no Cosmos requests. WithConsistency applies when the query is executed.");
    }

    private sealed class OfflineGalaxy(CosmosClient client)
        : Galaxy<ExampleEntity>(client, "example", "documents", ["/pk"], new UniverseOptions().WithAutoProvisioning(false));

    private sealed record ExampleEntity : CosmicEntity;
}
