using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OneOf;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.External.Client.MergeEntities;
using Reimaginate.DataHub.Requests.External.Client.MergeUntrackedEntities;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.CalculateSourceEntityUpdates;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.Requests.Internal.CreateChangeTrackingEntry;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;
using Reimaginate.DataHub.Requests.Internal.MergeExistingEntities;
using Reimaginate.DataHub.Requests.Internal.MergeExistingUntrackedEntities;
using Reimaginate.DataHub.Requests.Internal.MergeNewEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessNewEntity;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdatedEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdatedUntrackedEntities;
using Reimaginate.DataHub.Requests.Internal.RecordSyncEventsAgainstDataHubEntities;
using Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceResolutionPromises;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices.Cosmos;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

// Executes production merge, source-diff, materialization, and bulk-persistence code.
// Database reads/history storage, locks, rule approval, and external notification delivery are local boundaries.
internal sealed class MergeRegressionHarness : IDisposable
{
    private const string Source = "TestSource";
    private const string SourceType = "Reservation";
    private static readonly DateTimeOffset InitialTime = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
    public static readonly DateTimeOffset ProcessingTime = InitialTime.AddHours(1);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private readonly object gate = new();
    private readonly List<ChangeTrackingEntry> history = [];
    private readonly ServiceProvider services;
    private readonly IMediator mediator;
    private readonly IIdService ids = Substitute.For<IIdService>();
    private readonly ITimeService time = Substitute.For<ITimeService>();
    private readonly IProcessingLockService locks;
    private readonly AsyncLocal<WriteBatch?> currentBatch = new();
    private TaskCompletionSource changed = NewSignal();
    private int nextId;
    private int successfulWrites;
    private readonly bool newestWriteFirst;

    public Dictionary<string, JObject> Stored { get; } = [];
    public List<Notification> Notifications { get; } = [];
    public List<(string EntityId, bool Confirmed)> NotificationObservations { get; } = [];
    public List<(string EntityId, bool OtherEntityWasPending)> CompletedWrites { get; } = [];
    public List<MergeSuccess> MergeSuccesses { get; } = [];
    public List<MergeFailure> MergeFailures { get; } = [];
    public EntityConfig? Config { get; set; }
    public Func<CheckPreMergeRulesRequest, CheckPreMergeRulesResponse>? PreMergeRule { get; init; }
    public string? FailEntity { get; init; }
    public Func<ChangeTrackingEntry, bool>? FailHistoryWrite { get; init; }
    public string? HoldEntityUntilAnotherCompletes { get; init; }
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(10);
    // Performance measurements use indexed reads and immediate mock writes. The
    // default regression mode retains controlled asynchronous completion ordering.
    public bool PerformanceMode { get; init; }
    private readonly HashSet<string> confirmedEntityIds = [];
    private readonly List<PendingWrite> allWrites = [];

    public int EntityWriteCount(string entityId)
    {
        lock (gate) return allWrites.Count(w => w.EntityId == entityId);
    }

    public Func<IRequest, Task>? BeforeRequest { get; set; }

    public MergeRegressionHarness(bool newestWriteFirst, IProcessingLockService? processingLocks = null)
    {
        this.newestWriteFirst = newestWriteFirst;
        locks = processingLocks ?? Substitute.For<IProcessingLockService>();
        ids.NewId<DataHubEntity>().Returns(_ => $"new-entity-{Interlocked.Increment(ref nextId)}");
        ids.NewId<ChangeTrackingEntry>().Returns(_ => $"change-{Interlocked.Increment(ref nextId)}");
        time.Now().Returns(ProcessingTime);
        if (processingLocks == null) locks.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromResult(new Reimaginate.ProcessingLockService.Response<List<ProcessingLock>>(true, [], null)));
        var container = Substitute.For<Container>();
        container.UpsertItemAsync(Arg.Any<JObject>(), Arg.Any<PartitionKey?>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => QueueWrite(call.ArgAt<JObject>(0)));
        var client = Substitute.For<CosmosClient>();
        client.GetContainer("regression", "bookings").Returns(container);
        var dataService = new DataHubEntityDataService(new CosmosDataServiceOptions<JObject>
        {
            CosmosClient = client, DatabaseName = "regression", ContainerName = "bookings",
            GetItemIdFunc = item => item.Value<string>("id")!
        });
        services = new ServiceCollection().AddSingleton<IDataHubEntityDataService>(dataService).BuildServiceProvider();
        mediator = new LocalMediator(RespondAsync);
    }

    public static DateTimeOffset SourceTime(int version) => InitialTime.AddMinutes(version);
    public static MergeEntityRequest Booked(string sourceId = "booking-1", int version = 1) => Update(sourceId, version, "Booked", 42m);
    public static MergeEntityRequest Cancelled(string sourceId = "booking-1", int version = 2) => Update(sourceId, version, "Cancelled", 0);

    private static MergeEntityRequest Update(string sourceId, int version, string status, decimal total) => new()
    {
        DataSource = Source, SourceEntityType = SourceType, SourceEntityId = sourceId, DataHubEntityType = "Booking",
        Data = new JObject
        {
            ["Total"] = total, ["createdOn"] = InitialTime, ["lastUpdated"] = SourceTime(version),
            ["BookingLines"] = new JArray(new JObject { ["Ref"] = "line-1", ["Status"] = status, ["Updated"] = SourceTime(version) })
        }
    };

    public void SeedBooking(string sourceId, string entityId)
    {
        var entity = (JObject)Update(sourceId, 0, "Pending", 10).Data.DeepClone();
        entity["id"] = entityId;
        entity["entityType"] = "Booking";
        entity["alternateKeys"] = JArray.FromObject(new[] { new AlternateKey("testsource.reservation", sourceId) });
        Stored.Add(entityId, entity);
        history.Add(InitialEntry(DataSources.DataHub, "Booking", entityId, entity));
        history.Add(InitialEntry(Source, SourceType, sourceId, entity));
    }

    private static ChangeTrackingEntry InitialEntry(string source, string type, string id, JObject data) => new()
    {
        id = $"initial/{source}/{type}/{id}",
        EntityId = id, EntityType = type, DataSource = source, EntryType = ChangeTrackingEntryTypes.Init,
        Timestamp = InitialTime, Data = ChangeTrackingHelper.StripBaseProperties((JObject)data.DeepClone())
    };

    // Model a valid registered alias in both the projection and its initial history.
    public void SeedSourceAlias(string sourceId, string entityId, string dataSource = Source, string sourceType = SourceType)
    {
        var entity = Stored[entityId];
        ((JArray)entity["alternateKeys"]!).Add(JToken.FromObject(new AlternateKey($"{dataSource}.{sourceType}".ToLowerInvariant(), sourceId)));
        var initial = history.Single(e => e.DataSource == DataSources.DataHub && e.EntityId == entityId && e.EntryType == ChangeTrackingEntryTypes.Init);
        initial.Data["alternateKeys"] = entity["alternateKeys"]!.DeepClone();
        history.Add(InitialEntry(dataSource, sourceType, sourceId, entity));
    }

    public List<ChangeTrackingEntry> HistoryFor(string source, string type, string id)
    {
        lock (gate) return history.Where(e => e.DataSource == source && e.EntityType == type && e.EntityId == id).Select(CloneEntry).ToList();
    }

    public void ForgetSourceHistory(string sourceId)
    {
        lock (gate) history.RemoveAll(e => e.DataSource == Source && e.EntityId == sourceId);
    }

    public void ForgetDestinationHistory(string entityId)
    {
        lock (gate) history.RemoveAll(e => e.DataSource == DataSources.DataHub && e.EntityId == entityId);
    }

    public async Task<List<RebaseEntityTrackingResult>> RebaseAsync(bool source, string? sourceEntityType = null, string? sourceDataSource = null)
    {
        if (source)
            return (await new ProcessRebaseSourceEntitiesRequestHandler(mediator, locks).HandleAsync(new()
            {
                DataSource = sourceDataSource ?? Source, EntityType = sourceEntityType ?? SourceType, EntityIds = ["booking-1"]
            }, Token)).Results;
        return (await new ProcessRebaseDataHubEntitiesRequestHandler(mediator, locks).HandleAsync(new()
        {
            EntityType = "Booking", EntityIds = ["entity-1"]
        }, Token)).Results;
    }

    public async Task<MergeEntitiesResponse> MergePublicAsync(bool tracked, List<MergeEntityRequest> requests)
    {
        var config = Substitute.For<IEntityConfigService>();
        config.GetEntityConfig(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Config!);
        var task = tracked
            ? new MergeEntitiesRequestHandler(mediator, config, locks).HandleAsync(new MergeEntitiesRequest
                { DataSource = Source, AgentId = "regression", CorrelationId = "regression", Requests = requests }, Token)
            : new MergeUntrackedEntitiesRequestHandler(mediator, config, locks).HandleAsync(new MergeUntrackedEntitiesRequest
                { DataSource = Source, CorrelationId = "regression", Requests = requests }, Token);
        return await task.WaitAsync(OperationTimeout, Token);
    }

    public async Task<List<MergeEntityResult>> MergeUpdatesAsync(bool tracked, List<MergeEntityRequest> requests)
    {
        var references = Resolve(requests.Select(r => new ExternalEntityReference
            { DataSource = Source, SourceEntityType = SourceType, EntityType = "Booking", EntityId = r.SourceEntityId }).ToList());
        if (!tracked)
        {
            var response = await new ProcessUpdatedUntrackedEntitiesRequestHandler(mediator, locks, time).HandleAsync(new()
            {
                DataSource = Source, SourceEntityType = SourceType, DataHubEntityType = "Booking", CorrelationId = "regression",
                MergeRequests = requests, ResolvedReferencedEntities = references, EntityConfig = Config!
            }, Token).WaitAsync(OperationTimeout, Token);
            return response.Results;
        }

        var changes = new List<ChangeTrackingEntry>();
        var working = references.ToDictionary(r => r.SourceEntityReference.EntityId,
            r => (JObject)Stored[r.DataHubEntityReference.EntityId].DeepClone());
        foreach (var request in requests)
        {
            var before = working[request.SourceEntityId];
            var after = (JObject)before.DeepClone();
            after.Merge(request.Data, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
            changes.Add(new ChangeTrackingEntry
            {
                EntityId = references.Single(r => r.SourceEntityReference.EntityId == request.SourceEntityId).DataHubEntityReference.EntityId,
                EntityType = "Booking", DataSource = DataSources.DataHub, EntryType = ChangeTrackingEntryTypes.Update,
                Timestamp = request.Data.Value<DateTimeOffset>("lastUpdated"),
                Data = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(before, after)!)
            });
            working[request.SourceEntityId] = after;
        }
        var trackedResponse = await new ProcessUpdatedEntitiesRequestHandler(ids, mediator, locks, time).HandleAsync(new()
        {
            DataSource = Source, SourceEntityType = SourceType, DataHubEntityType = "Booking", CorrelationId = "regression",
            MergeRequests = requests, ResolvedReferencedEntities = references, EntityConfig = Config!,
            ConvertedSourceEntityChanges = changes, SourceEntityChangesWithAlternateKeys = []
        }, Token).WaitAsync(OperationTimeout, Token);
        return trackedResponse.Results;
    }

    private List<ResolvedEntityReference> Resolve(List<ExternalEntityReference> references)
    {
        lock (gate)
        {
            var alternateKeyIndex = PerformanceMode
                ? Stored.Values.SelectMany(entity => entity["alternateKeys"]!.ToObject<List<AlternateKey>>()!
                    .Select(key => (Key: (key.Key, key.Value), Entity: entity))).ToLookup(item => item.Key, item => item.Entity)
                : null;
            return references.DistinctBy(r => (r.DataSource, r.SourceEntityType, r.EntityId)).Select(reference =>
            {
                var key = $"{reference.DataSource}.{reference.SourceEntityType}".ToLowerInvariant();
                var entity = alternateKeyIndex != null
                    ? alternateKeyIndex[(key, reference.EntityId)].SingleOrDefault()
                    : Stored.Values.SingleOrDefault(e => e["alternateKeys"]!.ToObject<List<AlternateKey>>()!
                        .Any(k => k.Key == key && k.Value == reference.EntityId));
                return entity == null ? null : new ResolvedEntityReference
                {
                    SourceEntityReference = reference,
                    DataHubEntityReference = new EntityReference { EntityType = "Booking", EntityId = entity.Value<string>("id")! }
                };
            }).OfType<ResolvedEntityReference>().ToList();
        }
    }

    private async Task<object> RespondAsync(IRequest request)
    {
        if (BeforeRequest != null) await BeforeRequest(request);
        switch (request)
        {
            case ResolveEntityReferencesRequest resolve:
                return new ResolveEntityReferencesResponse { Success = true, Results = Resolve(resolve.EntityReferences) };
            case GetDataHubEntitiesByIdRequest get:
                lock (gate) return new GetDataHubEntitiesByIdResponse
                    { Success = true, Results = (PerformanceMode
                        ? get.EntityIds.Distinct().Where(Stored.ContainsKey).Select(id => Stored[id])
                        : Stored.Values.Where(e => get.EntityIds.Contains(e.Value<string>("id")!)))
                        .Select(e => (JObject)e.DeepClone()).ToList() };
            case GetMaterializedEntitiesByIdRequest get:
                lock (gate) return new GetMaterializedEntitiesByIdResponse
                {
                    Results = get.EntityIds.Where(Stored.ContainsKey).Select(id => (JObject)Stored[id].DeepClone()).ToList()
                };
            case CommitRebaseCheckpointCommand checkpoint:
                lock (gate)
                {
                    checkpoint.EnsureLease?.Invoke();
                    var ids = checkpoint.EntriesToDelete.Select(entry => entry.id).ToHashSet();
                    if (checkpoint.EntriesToDelete.Any(entry => !history.Any(stored => stored.id == entry.id)))
                        return new CommitRebaseCheckpointResponse { FailureReason = "Stale checkpoint" };
                    history.RemoveAll(entry => ids.Contains(entry.id));
                    if (checkpoint.Baseline != null) history.Add(CloneEntry(checkpoint.Baseline));
                    return new CommitRebaseCheckpointResponse { Success = true };
                }
            case DeleteCosmosDocumentsCommand<ChangeTrackingEntry> delete:
                lock (gate)
                {
                    var idsToDelete = delete.Documents.Select(entry => entry.id).ToHashSet();
                    history.RemoveAll(entry => idsToDelete.Contains(entry.id));
                    return new DeleteCosmosDocumentsResponse<ChangeTrackingEntry> { Successes = delete.Documents, Failures = [] };
                }
            case GetTrackingEntriesQuery query:
                lock (gate) return new PagedResults<ChangeTrackingEntry> { Results = ReadHistory(query.DataSource, query.EntityType, query.EntityIds) };
            case Reimaginate.DataHub.DataAccess.Queries.GetSourceRebaseIdentities.GetSourceRebaseIdentitiesQuery query:
                lock (gate) return SourceRebaseIdentityTests.IdentityPairs(history, query);
            case GetAllTrackingEntriesForEntitiesRequest query:
                lock (gate) return new GetAllTrackingEntriesForEntitiesResponse { TrackingEntries = ReadHistory(query.DataSource, query.EntityType, query.EntityIds) };
            case CheckPreMergeRulesRequest check:
                return PreMergeRule?.Invoke(check) ?? new CheckPreMergeRulesResponse { Pass = true };
            case ProcessUpdatedUntrackedEntitiesRequest update:
                return await new ProcessUpdatedUntrackedEntitiesRequestHandler(mediator, locks, time).HandleAsync(update, Token);
            case ProcessUpdatedEntitiesRequest update:
                return await new ProcessUpdatedEntitiesRequestHandler(ids, mediator, locks, time).HandleAsync(update, Token);
            case MergeExistingUntrackedEntitiesRequest merge:
                return await new MergeExistingUntrackedEntitiesRequestHandler(ids, mediator).HandleAsync(merge, Token);
            case MergeExistingEntitiesRequest merge:
                return await new MergeExistingEntitiesRequestHandler(ids, mediator).HandleAsync(merge, Token);
            case MergeNewEntitiesRequest merge:
                return await new MergeNewEntitiesRequestHandler(mediator, time).HandleAsync(merge, Token);
            case ProcessNewEntityRequest create:
                return await new ProcessNewEntityRequestHandler(ids, mediator, time).HandleAsync(create, Token);
            case CalculateSourceEntityUpdatesRequest calculate:
                return await new CalculateSourceEntityUpdatesRequestHandler(mediator, time).HandleAsync(calculate, Token);
            case GetTrackedEntityRequest get:
                return await new GetTrackedEntityRequestHandler(mediator).HandleAsync(get, Token);
            case MaterializeDataHubEntityRequest materialize:
                return await new MaterializeDataHubEntityRequestHandler(mediator).HandleAsync(materialize, Token);
            case AddTrackedEntityChangeSetsRequest track:
                return await new AddTrackedEntityChangeSetsRequestHandler(ids, mediator).HandleAsync(track, Token);
            case CreateChangeTrackingEntryRequest track:
                return await new CreateChangeTrackingEntryRequestHandler(mediator, ids, time).HandleAsync(track, Token);
            case InitTrackedEntityRequest track:
                return await new InitTrackedEntityRequestHandler(ids, mediator, time).HandleAsync(track, Token);
            case InitTrackedEntitiesRequest track:
                return await new InitTrackedEntitiesRequestHandler(ids, mediator, time).HandleAsync(track, Token);
            case CreateCosmosDocumentsCommand<ChangeTrackingEntry> create:
                var createFailures = create.Documents.Where(e => FailHistoryWrite?.Invoke(e) == true).ToList();
                var createSuccesses = createFailures.Count == 0 ? create.Documents : create.Documents.Except(createFailures).ToList();
                SaveHistory(createSuccesses);
                return new CreateCosmosDocumentsResponse<ChangeTrackingEntry>
                {
                    Successes = createSuccesses,
                    Failures = createFailures.Select(e => new Reimaginate.DataHub.Models.DataAccessFailure<ChangeTrackingEntry>(e, new InvalidOperationException("Injected history write failure"))).ToList()
                };
            case UpsertCosmosDocumentsCommand<ChangeTrackingEntry> upsert:
                var upsertFailures = upsert.Documents.Where(e => FailHistoryWrite?.Invoke(e) == true).ToList();
                var upsertSuccesses = upsertFailures.Count == 0 ? upsert.Documents : upsert.Documents.Except(upsertFailures).ToList();
                SaveHistory(upsertSuccesses);
                return new UpsertCosmosDocumentsResponse<ChangeTrackingEntry>
                {
                    Successes = upsertSuccesses,
                    Failures = upsertFailures.Select(e => new Reimaginate.DataHub.Models.DataAccessFailure<ChangeTrackingEntry>(e, new InvalidOperationException("Injected history write failure"))).ToList()
                };
            case ResolveEntityReferenceResolutionPromisesRequest:
                return new ResolveEntityReferenceResolutionPromisesResponse { UpdatedDataHubEntities = [] };
            case UpsertDataHubEntitiesCommand upsert:
                return await PersistAsync(upsert);
            case DispatchNotificationsRequest dispatch:
                lock (gate)
                {
                    Notifications.AddRange(dispatch.Notifications);
                    foreach (var notification in dispatch.Notifications)
                    {
                        var id = notification switch
                        {
                            DataHubEntityUpdatedNotification n => n.DataHubEntityId,
                            DataHubEntityCreatedNotification n => n.DataHubEntityId,
                            _ => null
                        };
                        if (id != null) NotificationObservations.Add((id, PerformanceMode
                            ? confirmedEntityIds.Contains(id) : allWrites.Any(w => w.EntityId == id && w.Succeeded)));
                    }
                }
                return new DispatchNotificationsResponse { Success = true, Failures = [] };
            case LogSyncEventsRequest<MergeSuccess> success:
                lock (gate) MergeSuccesses.AddRange(success.SyncEvents);
                return new LogEventsResponse { Success = true, LogEntries = [] };
            case LogSyncEventsRequest<MergeFailure> failure:
                lock (gate) MergeFailures.AddRange(failure.SyncEvents);
                return new LogEventsResponse { Success = true, LogEntries = [] };
            case RecordSyncEventsAgainstDataHubEntitiesRequest<MergeFailure>:
                return new NullResponse();
            default:
                throw new InvalidOperationException($"Unhandled production request at test boundary: {request.GetType().Name}");
        }
    }

    private List<ChangeTrackingEntry> ReadHistory(string source, string type, List<string> entityIds)
    {
        var indexedIds = PerformanceMode ? entityIds.ToHashSet() : null;
        return history.Where(e => e.DataSource == source && e.EntityType == type &&
            (indexedIds != null ? indexedIds.Contains(e.EntityId) : entityIds.Contains(e.EntityId))).Select(CloneEntry).ToList();
    }

    private void SaveHistory(List<ChangeTrackingEntry> entries)
    {
        lock (gate) history.AddRange(entries.Select(CloneEntry));
    }

    private static ChangeTrackingEntry CloneEntry(ChangeTrackingEntry entry) => new()
    {
        id = entry.id, EntityId = entry.EntityId, EntityType = entry.EntityType, DataSource = entry.DataSource,
        EntryType = entry.EntryType, Timestamp = entry.Timestamp, _ts = entry._ts, Data = (JObject)entry.Data.DeepClone()
    };

    private Task<ItemResponse<JObject>> QueueWrite(JObject document)
    {
        lock (gate)
        {
            var write = new PendingWrite((JObject)document.DeepClone(), currentBatch.Value ?? throw new InvalidOperationException("Missing write batch"));
            allWrites.Add(write);
            Pulse();
            if (PerformanceMode) Complete(write);
            return write.Completion.Task;
        }
    }

    internal async Task<UpsertDataHubEntitiesResponse> PersistAsync(UpsertDataHubEntitiesCommand command)
    {
        var batch = new WriteBatch();
        var previous = currentBatch.Value;
        currentBatch.Value = batch;
        Task<UpsertDataHubEntitiesResponse> persistence;
        try
        {
            // Let production launch its currently available operations before selecting a completion.
            // This captures concurrent dispatch without waiting for an assumed write count.
            persistence = new UpsertDataHubEntitiesCommandHandler(services).HandleAsync(command, Token);
        }
        finally { currentBatch.Value = previous; }

        while (!persistence.IsCompleted)
        {
            PendingWrite? next;
            Task wait;
            lock (gate)
            {
                var eligible = allWrites.Where(w => w.Batch == batch && !w.Settled &&
                    (w.EntityId != HoldEntityUntilAnotherCompletes || successfulWrites > 0));
                next = (newestWriteFirst ? eligible.OrderByDescending(w => w.Version) : eligible.OrderBy(w => w.Version)).FirstOrDefault();
                wait = changed.Task;
            }
            if (next != null) Complete(next);
            else await Task.WhenAny(persistence, wait).WaitAsync(TimeSpan.FromSeconds(5), Token);
        }
        return await persistence;
    }

    private void Complete(PendingWrite write)
    {
        var response = Substitute.For<ItemResponse<JObject>>();
        response.Resource.Returns(write.Document);
        lock (gate)
        {
            write.Settled = true;
            if (write.EntityId != FailEntity)
            {
                Stored[write.EntityId] = (JObject)write.Document.DeepClone();
                write.Succeeded = true;
                successfulWrites++;
                confirmedEntityIds.Add(write.EntityId);
                CompletedWrites.Add((write.EntityId, !PerformanceMode && allWrites.Any(other => !other.Settled && other.EntityId != write.EntityId)));
            }
            Pulse();
        }
        if (write.Succeeded) write.Completion.SetResult(response);
        else write.Completion.SetException(new InvalidOperationException("Controlled database write failure"));
    }

    private void Pulse()
    {
        var signal = changed;
        changed = NewSignal();
        signal.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class WriteBatch;
    private sealed class PendingWrite(JObject document, WriteBatch batch)
    {
        public JObject Document { get; } = document;
        public string EntityId => Document.Value<string>("id")!;
        public DateTimeOffset Version => Document.SelectToken("BookingLines[0].Updated")!.AsDateTimeOffset()!.Value;
        public WriteBatch Batch { get; } = batch;
        public bool Settled { get; set; }
        public bool Succeeded { get; set; }
        public TaskCompletionSource<ItemResponse<JObject>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Dispose() => services.Dispose();

    private sealed class LocalMediator(Func<IRequest, Task<object>> respond) : IMediator
    {
        public async Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            try { return (TResponse)await respond(request); }
            catch (Exception ex) { return ex; }
        }
        public async Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
        {
            try { return await respond(request); }
            catch (Exception ex) { return ex; }
        }
        public async Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            try { return ((TResponse)await respond(request), null); }
            catch (Exception ex) { exceptionHandler?.Invoke(ex); return (default, ex); }
        }
        public Task<object> SendAndHandleExceptions<TRequest>(TRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<TResponse> SendAndHandleExceptions<TResponse>(IRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
            => throw new NotSupportedException();
    }
}
