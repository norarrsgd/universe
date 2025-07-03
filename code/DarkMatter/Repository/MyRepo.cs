using Microsoft.Azure.Cosmos;
using Universe;
using DarkMatter.Models;

namespace DarkMatter.Repository
{
#if DEBUG
    public class MyRepo(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey) : Galaxy<MyObject>(client, database, container, partitionKey, recordQueries: true)
    {
#else
    public class MyRepo(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey) : Galaxy<MyObject>(client, database, container, partitionKey)
    {
#endif
    }
}
