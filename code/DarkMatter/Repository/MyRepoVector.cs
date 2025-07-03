using Microsoft.Azure.Cosmos;
using Universe;
using DarkMatter.Models;

namespace DarkMatter.Repository
{
#if DEBUG
    public class MyRepoVector(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey, IReadOnlyDictionary<string, VectorIndexType> vectorPolicy) : Galaxy<MyObjectVector>(client, database, container, partitionKey, vectorPolicy, true)
    {
#else
    public class MyRepoVector(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey, IReadOnlyDictionary<string, VectorIndexType> vectorPolicy) : Galaxy<MyObjectVector>(client, database, container, partitionKey, vectorPolicy)
    {
#endif
    }
}
