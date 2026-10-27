using System.Collections;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;

namespace Universe.Tests.Helpers;

internal sealed class QueryTestContainer : Container
{
    public object ReadItemResult { get; set; }
    public object ReplaceItemResult { get; set; }
    public IReadOnlyList<object> QueryItems { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<object>> QueryPages { get; set; }
    public int QueryPageReads { get; private set; }
    public bool QueryIteratorDisposed { get; private set; }
    public int? ThrowOnQueryPage { get; set; }
    public QueryDefinition LastQuery { get; private set; }
    public QueryRequestOptions LastQueryOptions { get; private set; }
    public string ETag { get; set; } = "fake-etag";
    public CosmosException ReadException { get; set; }
    public int ReadItemCalls { get; private set; }
    public int QueryCalls { get; private set; }

    public override string Id => "container";
    public override Database Database => throw new NotSupportedException();
    public override Conflicts Conflicts => throw new NotSupportedException();
    public override Scripts Scripts => throw new NotSupportedException();

    public override Task<ItemResponse<T>> ReadItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
    {
        ReadItemCalls++;
        if (ReadException is not null)
            throw ReadException;
        return Task.FromResult<ItemResponse<T>>(new FakeItemResponse<T>((T)ReadItemResult, 9.5, ETag));
    }

    public override Task<ItemResponse<T>> ReplaceItemAsync<T>(T item, string id, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        => Task.FromResult<ItemResponse<T>>(new FakeItemResponse<T>((T)ReplaceItemResult, 6.5));

    public override Task<ItemResponse<T>> DeleteItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        => Task.FromResult<ItemResponse<T>>(new FakeItemResponse<T>(default, 4.5));

    public override FeedIterator<T> GetItemQueryIterator<T>(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null)
    {
        QueryCalls++;
        LastQuery = queryDefinition;
        LastQueryOptions = requestOptions;
        return new FakeFeedIterator<T>(QueryPages ?? [QueryItems], this);
    }

    public override Task<ItemResponse<T>> CreateItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        => Task.FromResult<ItemResponse<T>>(new FakeItemResponse<T>(item, 5.5));

    public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey)
        => throw new NotSupportedException();

    public override Task<ContainerResponse> ReadContainerAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> ReadContainerStreamAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ContainerResponse> ReplaceContainerAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> ReplaceContainerStreamAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ContainerResponse> DeleteContainerAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> DeleteContainerStreamAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ThroughputResponse> ReadThroughputAsync(RequestOptions requestOptions, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ThroughputResponse> ReplaceThroughputAsync(int throughput, RequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ThroughputResponse> ReplaceThroughputAsync(ThroughputProperties throughputProperties, RequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> CreateItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> ReadItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> UpsertItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ItemResponse<T>> UpsertItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> ReplaceItemStreamAsync(Stream streamPayload, string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<FeedResponse<T>> ReadManyItemsAsync<T>(IReadOnlyList<(string id, PartitionKey partitionKey)> items, ReadManyRequestOptions readManyRequestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> ReadManyItemsStreamAsync(IReadOnlyList<(string id, PartitionKey partitionKey)> items, ReadManyRequestOptions readManyRequestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ItemResponse<T>> PatchItemAsync<T>(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> patchOperations, PatchItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> PatchItemStreamAsync(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> patchOperations, PatchItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override Task<ResponseMessage> DeleteItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override FeedIterator GetItemQueryStreamIterator(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotSupportedException();
    public override FeedIterator GetItemQueryStreamIterator(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotSupportedException();
    public override FeedIterator<T> GetItemQueryIterator<T>(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotSupportedException();
    public override FeedIterator GetItemQueryStreamIterator(FeedRange feedRange, QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotSupportedException();
    public override FeedIterator<T> GetItemQueryIterator<T>(FeedRange feedRange, QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotSupportedException();
    public override IOrderedQueryable<T> GetItemLinqQueryable<T>(bool allowSynchronousQueryExecution = false, string continuationToken = null, QueryRequestOptions requestOptions = null, CosmosLinqSerializerOptions linqSerializerOptions = null) => throw new NotSupportedException();
    public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangesHandler<T> onChangesDelegate) => throw new NotSupportedException();
    public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(string processorName, ChangesEstimationHandler estimationDelegate, TimeSpan? estimationPeriod = null) => throw new NotSupportedException();
    public override ChangeFeedEstimator GetChangeFeedEstimator(string processorName, Container leaseContainer) => throw new NotSupportedException();
    public override Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public override FeedIterator GetChangeFeedStreamIterator(ChangeFeedStartFrom changeFeedStartFrom, ChangeFeedMode changeFeedMode, ChangeFeedRequestOptions changeFeedRequestOptions = null) => throw new NotSupportedException();
    public override FeedIterator<T> GetChangeFeedIterator<T>(ChangeFeedStartFrom changeFeedStartFrom, ChangeFeedMode changeFeedMode, ChangeFeedRequestOptions changeFeedRequestOptions = null) => throw new NotSupportedException();
    public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangeFeedHandler<T> onChangesDelegate) => throw new NotSupportedException();
    public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(string processorName, ChangeFeedHandlerWithManualCheckpoint<T> onChangesDelegate) => throw new NotSupportedException();
    public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder(string processorName, ChangeFeedStreamHandler onChangesDelegate) => throw new NotSupportedException();
    public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint(string processorName, ChangeFeedStreamHandlerWithManualCheckpoint onChangesDelegate) => throw new NotSupportedException();

    private sealed class FakeFeedIterator<T>(IReadOnlyList<IReadOnlyList<object>> pages, QueryTestContainer container) : FeedIterator<T>
    {
        private int _page;

        public override bool HasMoreResults => _page < pages.Count;

        public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            container.QueryPageReads++;
            if (container.ThrowOnQueryPage == container.QueryPageReads)
                throw new InvalidOperationException("Scripted query failure");

            return Task.FromResult<FeedResponse<T>>(new FakeFeedResponse<T>(pages[_page++].Cast<T>().ToArray(), 7.25));
        }

        protected override void Dispose(bool disposing)
        {
            container.QueryIteratorDisposed = true;
            base.Dispose(disposing);
        }
    }
}

internal sealed class FakeItemResponse<T>(T resource, double requestCharge, string eTag = "fake-etag") : ItemResponse<T>
{
    public override T Resource => resource;
    public override double RequestCharge => requestCharge;
    public override string ETag => eTag;
    public override HttpStatusCode StatusCode => HttpStatusCode.OK;
}

internal sealed class FakeFeedResponse<T>(IReadOnlyList<T> items, double requestCharge) : FeedResponse<T>
{
    public override string ContinuationToken => null;
    public override int Count => items.Count;
    public override string IndexMetrics => string.Empty;
    public override Headers Headers => null;
    public override IEnumerable<T> Resource => items;
    public override HttpStatusCode StatusCode => HttpStatusCode.OK;
    public override CosmosDiagnostics Diagnostics => null;
    public override double RequestCharge => requestCharge;
    public override IEnumerator<T> GetEnumerator() => items.GetEnumerator();
}

