using Microsoft.Azure.Cosmos;
using Universe;
using DarkMatter.Models;

namespace DarkMatter.Repository
{
#if DEBUG
    public class MyRepoVector(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey) : Galaxy<MyObjectVector>(client, database, container, partitionKey, UniverseOptions.WithFilePersistence(), true)
    {
#else
    public class MyRepoVector(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey) : Galaxy<MyObjectVector>(client, database, container, partitionKey, UniverseOptions.WithFilePersistence())
    {
#endif
    }
}