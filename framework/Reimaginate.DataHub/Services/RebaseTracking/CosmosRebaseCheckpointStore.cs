using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Cosmos;

namespace Reimaginate.DataHub.Services.RebaseTracking;

internal interface IRebaseCheckpointStore
{
    Task<CommitRebaseCheckpointResponse> CommitAsync(CommitRebaseCheckpointCommand request, CancellationToken cancellationToken);
}

internal sealed class CosmosRebaseCheckpointStore(CosmosDataServiceOptions<ChangeTrackingEntry> options) : IRebaseCheckpointStore
{
    internal const int MaxOperations = 100;
    // Leave room for the SDK's binary batch envelope, headers and partition key.
    internal const int MaxPayloadBytes = 2 * 1024 * 1024 - 64 * 1024;
    private readonly SemaphoreSlim metadataGate = new(1, 1);
    private IReadOnlyList<string> partitionPaths;

    public async Task<CommitRebaseCheckpointResponse> CommitAsync(CommitRebaseCheckpointCommand request, CancellationToken cancellationToken)
    {
        var deleting = request.EntriesToDelete ?? [];
        var baseline = request.Baseline;
        var documents = deleting.Concat(baseline == null ? [] : new[] { baseline }).ToList();
        if (documents.Count == 0) return new() { Success = true };
        if (documents.Count > MaxOperations) return Large();
        if (documents.Any(item => item == null || string.IsNullOrWhiteSpace(item.id)) ||
            documents.Select(item => item.id).Distinct().Count() != documents.Count ||
            documents.Select(item => (item.DataSource, item.EntityType, item.EntityId)).Distinct().Count() != 1 ||
            (baseline != null && (baseline.EntryType != ChangeTrackingEntryTypes.Init || baseline.Data == null)))
            return Failed("Invalid atomic rebase checkpoint; original history was preserved.");
        if (options.CosmosClient == null) return Failed("Atomic rebasing requires a Cosmos transaction-capable tracking store.");

        var container = options.CosmosClient.GetContainer(options.DatabaseName, options.ContainerName);
        var serializer = options.CosmosClient.ClientOptions.Serializer ?? new DataHubDataSerializer();
        var baselineBytes = Array.Empty<byte>();
        JObject baselineJson = null;
        PartitionKey partition;
        try
        {
            var paths = await GetPartitionPathsAsync(container, cancellationToken);
            if (baseline != null) options.SetItemPartitionKeyFunc?.Invoke(baseline);
            var keys = new List<PartitionKey>();
            foreach (var document in documents)
            {
                if (options.GetItemIdFunc != null && options.GetItemIdFunc(document) != document.id)
                    return Failed("Configured tracking IDs do not match stored document IDs; checkpoint was not written.");
                using var stream = serializer.ToStream(document);
                using var bytes = new MemoryStream();
                await stream.CopyToAsync(bytes, cancellationToken);
                var payload = bytes.ToArray();
                var json = Parse(payload);
                keys.Add(BuildPartition(json, paths));
                if (ReferenceEquals(document, baseline))
                {
                    baselineBytes = payload;
                    baselineJson = json;
                }
            }

            if (keys.Any(key => !key.Equals(keys[0])))
                return Failed("Tracking history spans logical partitions; atomic rebase was not performed.");
            partition = keys[0];
            var estimatedSize = baselineBytes.Length + documents.Sum(item =>
                1024 + Encoding.UTF8.GetByteCount(item.id) + Encoding.UTF8.GetByteCount(item._etag ?? string.Empty)) +
                Encoding.UTF8.GetByteCount(partition.ToString()) * documents.Count;
            if (estimatedSize > MaxPayloadBytes) return Large();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Failed($"Cannot prepare atomic rebase checkpoint: {ex.Message}"); }

        var batch = container.CreateTransactionalBatch(partition);
        using var baselinePayload = new MemoryStream(baselineBytes);
        if (baseline != null) batch.CreateItemStream(baselinePayload);
        foreach (var entry in deleting)
            batch.DeleteItem(entry.id, new TransactionalBatchItemRequestOptions { IfMatchEtag = entry._etag });
        request.EnsureLease?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode) return new() { Success = true };
            if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge) return Large();
            // A conflict can be an earlier attempt with a lost response. Check, never blindly replay.
            if (response.StatusCode == HttpStatusCode.Conflict || (int)response.StatusCode >= 500 ||
                response.StatusCode == HttpStatusCode.RequestTimeout)
                return await VerifyAsync(container, partition, baselineJson, deleting, $"Cosmos returned {(int)response.StatusCode}");
            return Failed($"Atomic rebase checkpoint failed ({(int)response.StatusCode}); no operations in this checkpoint were committed.");
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge) { return Large(); }
        catch (Exception ex)
        {
            // Execute may commit despite timeout/cancellation. Read back under an independent, bounded token.
            return await VerifyAsync(container, partition, baselineJson, deleting, ex.Message);
        }
    }

    private async Task<IReadOnlyList<string>> GetPartitionPathsAsync(Container container, CancellationToken cancellationToken)
    {
        if (partitionPaths != null) return partitionPaths;
        await metadataGate.WaitAsync(cancellationToken);
        try
        {
            partitionPaths ??= (await container.ReadContainerAsync(cancellationToken: cancellationToken)).Resource.PartitionKeyPaths.ToList();
            return partitionPaths;
        }
        finally { metadataGate.Release(); }
    }

    private static PartitionKey BuildPartition(JObject document, IReadOnlyList<string> paths)
    {
        var builder = new PartitionKeyBuilder();
        foreach (var path in paths)
        {
            JToken token = document;
            foreach (var part in path.TrimStart('/').Split('/'))
                token = (token as JObject)?[part.Replace("~1", "/").Replace("~0", "~")];
            switch (token?.Type)
            {
                case null: builder.AddNoneType(); break;
                case JTokenType.Null: builder.AddNullValue(); break;
                case JTokenType.String: builder.Add(token.Value<string>()); break;
                case JTokenType.Boolean: builder.Add(token.Value<bool>()); break;
                case JTokenType.Integer:
                case JTokenType.Float: builder.Add(token.Value<double>()); break;
                default: throw new InvalidOperationException("Unsupported tracking partition-key value.");
            }
        }
        return builder.Build();
    }

    private static async Task<CommitRebaseCheckpointResponse> VerifyAsync(Container container, PartitionKey partition,
        JObject baseline, List<ChangeTrackingEntry> deleting, string reason)
    {
        using var verification = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pending = VerifyCoreAsync(container, partition, baseline, deleting, reason, verification.Token);
        try { return await pending.WaitAsync(verification.Token); }
        catch (Exception ex)
        {
            _ = ObserveAsync(pending);
            return Unknown($"{reason}; confirmation failed: {ex.Message}");
        }
    }

    private static async Task ObserveAsync(Task pending)
    {
        try { await pending; }
        catch { }
    }

    private static async Task<CommitRebaseCheckpointResponse> VerifyCoreAsync(Container container, PartitionKey partition,
        JObject baseline, List<ChangeTrackingEntry> deleting, string reason, CancellationToken cancellationToken)
    {
        try
        {
            if (baseline != null)
            {
                var response = await ReadAsync(container, baseline.Value<string>("id"), partition, cancellationToken);
                using (response)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        using var bytes = new MemoryStream();
                        await response.Content.CopyToAsync(bytes, cancellationToken);
                        var stored = Parse(bytes.ToArray());
                        if (baseline.Properties().Where(property => property.Name != "_etag" && property.Name != "_ts")
                            .All(property => JToken.DeepEquals(property.Value, stored[property.Name])))
                            return new() { Success = true };
                    }
                }
            }
            else
            {
                foreach (var entry in deleting)
                {
                    using var response = await ReadAsync(container, entry.id, partition, cancellationToken);
                    if (response.StatusCode != HttpStatusCode.NotFound) return Unknown(reason);
                }
                return new() { Success = true };
            }
        }
        catch (Exception ex) { reason += $"; confirmation failed: {ex.Message}"; }
        return Unknown(reason);
    }

    private static async Task<ResponseMessage> ReadAsync(Container container, string id, PartitionKey partition, CancellationToken cancellationToken)
    {
        var pending = container.ReadItemStreamAsync(id, partition, cancellationToken: cancellationToken);
        try { return await pending.WaitAsync(cancellationToken); }
        catch { _ = DisposeLateAsync(pending); throw; }
    }

    private static async Task DisposeLateAsync(Task<ResponseMessage> task)
    {
        try { (await task)?.Dispose(); }
        catch { }
    }

    private static JObject Parse(byte[] payload)
    {
        using var text = new StreamReader(new MemoryStream(payload));
        using var json = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(json);
    }

    private static CommitRebaseCheckpointResponse Large() => new() { TooLarge = true, FailureReason = "Atomic checkpoint exceeds Cosmos transaction limits." };
    private static CommitRebaseCheckpointResponse Failed(string reason) => new() { FailureReason = reason };
    private static CommitRebaseCheckpointResponse Unknown(string reason) => new()
    {
        OutcomeUnknown = true,
        FailureReason = $"Atomic checkpoint outcome could not be confirmed; no automatic retry was attempted. {reason}"
    };
}
