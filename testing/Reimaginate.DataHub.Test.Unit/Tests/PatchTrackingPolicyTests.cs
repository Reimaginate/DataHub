using System.Collections.Concurrent;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OneOf;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

[Trait("Category", "TrackingPolicy")]
public class PatchTrackingPolicyTests
{
    [Theory]
    [InlineData("internal", false)]
    [InlineData("internal", true)]
    [InlineData("internal-cached", false)]
    [InlineData("internal-cached", true)]
    [InlineData("single", false)]
    [InlineData("single", true)]
    [InlineData("batch", false)]
    [InlineData("batch", true)]
    public async Task Explicit_policy_overrides_stray_history_while_reparenting_and_notifying(string path, bool hasInit)
    {
        var db = new Harness();
        db.Configs.GetEntityConfig("Booking", Arg.Any<CancellationToken>(), cacheMissing: true)
            .Returns(new EntityConfig { EntityType = "Booking", DoNotTrack = true });
        db.Seed("Booking", "booking", hasInit);
        var before = JArray.FromObject(db.History).ToString();

        await db.Patch(path, "Booking", "booking");

        db.Stored["booking"].SelectToken("Patron.EntityId")!.Value<string>().Should().Be("survivor");
        JArray.FromObject(db.History).ToString().Should().Be(before);
        db.Notifications.OfType<DataHubEntityUpdatedNotification>().Should().ContainSingle(n => n.DataHubEntityId == "booking");
        db.Requests.Should().NotContain(r => r is GetAllTrackingEntriesForEntitiesRequest);
        await db.Configs.Received(1).GetEntityConfig("Booking", Arg.Any<CancellationToken>(), cacheMissing: true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task Unrestricted_entity_without_a_supplied_cache_loads_history_and_tracks(bool? policy)
    {
        var db = new Harness();
        db.Configs.GetEntityConfig("Booking", Arg.Any<CancellationToken>(), cacheMissing: true)
            .Returns(policy == null ? null : new EntityConfig { DoNotTrack = policy });
        db.Seed("Booking", "booking", true);
        await db.Patch("internal", "Booking", "booking");
        db.History.Count.Should().Be(2);
        db.Requests.OfType<GetAllTrackingEntriesForEntitiesRequest>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mixed_batch_respects_owner_and_item_suppression(bool suppressBatch)
    {
        var db = new Harness();
        db.Configs.GetEntityConfig("Booking", Arg.Any<CancellationToken>(), cacheMissing: true).Returns(new EntityConfig { DoNotTrack = true });
        db.Seed("Booking", "booking", true);
        db.Seed("Performance", "suppressed", true);
        db.Seed("Performance", "tracked", true);
        var suppressed = Item("Performance", "suppressed");
        suppressed.DoNotTrack = true;
        var response = await db.Batch.HandleAsync(new ProcessPatchEntitiesRequest
        {
            Requests = [Item("Booking", "booking"), suppressed, Item("Performance", "tracked")],
            DoNotTrack = suppressBatch
        }, TestContext.Current.CancellationToken);

        response.Results.Should().OnlyContain(r => r.Success);
        db.Stored.Values.Should().OnlyContain(e => e.SelectToken("Patron.EntityId")!.Value<string>() == "survivor");
        db.History.Count.Should().Be(suppressBatch ? 3 : 4);
        if (!suppressBatch) db.History.Last().EntityId.Should().Be("tracked");
        db.Requests.OfType<GetAllTrackingEntriesForEntitiesRequest>().Should().ContainSingle().Which.EntityType.Should().Be("Performance");
        await db.Configs.Received(1).GetEntityConfig("Booking", Arg.Any<CancellationToken>(), cacheMissing: true);
        await db.Configs.Received(1).GetEntityConfig("Performance", Arg.Any<CancellationToken>(), cacheMissing: true);
        db.Notifications.Should().HaveCount(3);
    }

    [Fact]
    public async Task Suppression_is_per_operation_and_does_not_disable_later_tracking()
    {
        var db = new Harness();
        db.Seed("Booking", "booking", true);
        var first = Item("Booking", "booking");
        first.DoNotTrack = true;
        (await db.Single.HandleAsync(first, TestContext.Current.CancellationToken)).Success.Should().BeTrue();
        db.History.Should().ContainSingle();
        var next = Item("Booking", "booking");
        next.Operations[0].Value = "next-survivor";
        (await db.Single.HandleAsync(next, TestContext.Current.CancellationToken)).Success.Should().BeTrue();
        db.History.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("internal")]
    [InlineData("batch")]
    public async Task Policy_lookup_failure_does_not_write_or_notify(string path)
    {
        var db = new Harness();
        db.Seed("Booking", "booking", true);
        db.Configs.GetEntityConfig("Booking", Arg.Any<CancellationToken>(), cacheMissing: true)
            .Returns(Task.FromException<EntityConfig>(new InvalidOperationException("configuration unavailable")));
        var results = path == "internal"
            ? new[] { await db.Single.HandleAsync(Item("Booking", "booking"), TestContext.Current.CancellationToken) }
            : (await db.Batch.HandleAsync(new ProcessPatchEntitiesRequest { Requests = [Item("Booking", "booking")] }, TestContext.Current.CancellationToken)).Results.ToArray();
        results.Should().OnlyContain(r => !r.Success && r.FailureReason.Contains("configuration unavailable"));
        db.Stored["booking"].SelectToken("Patron.EntityId")!.Value<string>().Should().Be("merged-away");
        db.History.Should().ContainSingle();
        db.Notifications.Should().BeEmpty();
    }

    [Theory]
    [InlineData("internal")]
    [InlineData("batch")]
    public async Task Untracked_persistence_failure_is_reported_without_notification(string path)
    {
        var db = new Harness { FailWrites = true };
        db.Configs.GetEntityConfig("Booking", Arg.Any<CancellationToken>(), cacheMissing: true).Returns(new EntityConfig { DoNotTrack = true });
        db.Seed("Booking", "booking", false);
        var results = path == "internal"
            ? new[] { await db.Single.HandleAsync(Item("Booking", "booking"), TestContext.Current.CancellationToken) }
            : (await db.Batch.HandleAsync(new ProcessPatchEntitiesRequest { Requests = [Item("Booking", "booking")] }, TestContext.Current.CancellationToken)).Results.ToArray();
        results.Should().OnlyContain(r => !r.Success);
        db.Notifications.Should().BeEmpty();
        db.History.Should().ContainSingle();
    }

    [Fact]
    public async Task Source_patch_keeps_history_reconstruction_even_for_same_named_untracked_DataHub_type()
    {
        var db = new Harness();
        db.Configs.GetEntityConfig("Booking", Arg.Any<CancellationToken>(), cacheMissing: true).Returns(new EntityConfig { DoNotTrack = true });
        db.Seed("Booking", "source-booking", true, "Source");
        var item = Item("Booking", "source-booking");
        item.DataSource = "Source";
        var response = await db.Batch.HandleAsync(new ProcessPatchEntitiesRequest { Requests = [item] }, TestContext.Current.CancellationToken);
        response.Results.Should().OnlyContain(r => r.Success);
        db.Requests.Should().Contain(r => r is GetTrackedEntityRequest);
        db.History.Should().HaveCount(2);
        db.History.Last().DataSource.Should().Be("Source");
        db.Configs.ReceivedCalls().Should().BeEmpty();
    }

    private static ProcessPatchEntityRequest Item(string type, string id) => new()
    {
        RequestId = id, DataSource = DataSources.DataHub, EntityType = type, EntityId = id,
        DispatchNotifications = true,
        Operations = [new Patch { Operation = "set", Path = "Patron.EntityId", Value = "survivor" }]
    };

    private sealed class Harness
    {
        public readonly IEntityConfigService Configs = Substitute.For<IEntityConfigService>();
        public readonly ConcurrentDictionary<string, JObject> Stored = new();
        public readonly List<ChangeTrackingEntry> History = [];
        public readonly List<Notification> Notifications = [];
        public readonly ConcurrentBag<IRequest> Requests = [];
        public readonly ProcessPatchEntityRequestHandler Single;
        public readonly ProcessPatchEntitiesRequestHandler Batch;
        private readonly LocalMediator mediator;
        public bool FailWrites { get; init; }

        public Harness()
        {
            var locks = Substitute.For<IProcessingLockService>();
            locks.WaitForLockAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
                .Returns(new Response<ProcessingLock>(true, new ProcessingLock(), null));
            locks.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
                .Returns(new Response<List<ProcessingLock>>(true, [], null));
            var time = Substitute.For<ITimeService>();
            time.Now().Returns(DateTimeOffset.Parse("2030-01-02T00:00:00Z"));
            mediator = new LocalMediator(Respond);
            Single = new(mediator, locks, time, Configs);
            Batch = new(mediator, locks, time, Configs);
        }

        public void Seed(string type, string id, bool hasInit, string source = DataSources.DataHub)
        {
            Stored[id] = new JObject { ["id"] = id, ["entityType"] = type,
                ["lastUpdated"] = DateTimeOffset.Parse("2030-01-01T00:00:00Z"),
                ["Patron"] = new JObject { ["EntityType"] = "Contact", ["EntityId"] = "merged-away" } };
            History.Add(new ChangeTrackingEntry { id = "seed-" + id, DataSource = source, EntityType = type, EntityId = id,
                EntryType = hasInit ? ChangeTrackingEntryTypes.Init : ChangeTrackingEntryTypes.Update, Data = new JObject() });
        }

        public async Task Patch(string path, string type, string id)
        {
            var item = Item(type, id);
            if (path.StartsWith("internal", StringComparison.Ordinal))
            {
                if (path == "internal-cached")
                    item.Cache = new Dictionary<string, object>
                    {
                        ["TrackingEntries"] = History.ToList(),
                        ["DataHubEntities"] = new List<JObject> { Stored[id] }
                    };
                (await Single.HandleAsync(item, TestContext.Current.CancellationToken)).Success.Should().BeTrue();
            }
            else
            {
                var request = new PatchEntityRequest { DataSource = item.DataSource, EntityType = type, EntityId = id,
                    Operations = item.Operations, DispatchNotifications = true };
                if (path == "single")
                    (await new Reimaginate.DataHub.Requests.External.Client.PatchEntity.PatchEntityRequestHandler(mediator).HandleAsync(request, TestContext.Current.CancellationToken)).Success.Should().BeTrue();
                else
                    (await new Reimaginate.DataHub.Requests.External.Client.PatchEntities.PatchEntitiesRequestHandler(mediator)
                        .HandleAsync(new PatchEntitiesRequest { Requests = [request] }, TestContext.Current.CancellationToken)).Success.Should().BeTrue();
            }
        }

        private async Task<object> Respond(IRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            switch (request)
            {
                case ProcessPatchEntitiesRequest r: return await Batch.HandleAsync(r, ct);
                case ProcessPatchEntityRequest r: return await Single.HandleAsync(r, ct);
                case GetMaterializedEntitiesByIdRequest r:
                    return new GetMaterializedEntitiesByIdResponse { Results = r.EntityIds.Select(id => (JObject)Stored[id].DeepClone()).ToList() };
                case GetAllTrackingEntriesForEntitiesRequest r:
                    return new GetAllTrackingEntriesForEntitiesResponse { TrackingEntries = History.Where(e => e.DataSource == r.DataSource && e.EntityType == r.EntityType && r.EntityIds.Contains(e.EntityId)).ToList() };
                case GetTrackedEntityRequest r: return (JObject)Stored[r.EntityId].DeepClone();
                case AddTrackedEntityChangeSetsRequest r:
                    foreach (var item in r.Requests) Add(item);
                    return new AddTrackedEntityChangeSetsResponse { Successes = [], Failures = [] };
                case AddTrackedEntityChangeSetRequest r: return Add(r);
                case UpsertDataHubEntitiesCommand r:
                    if (FailWrites) return new UpsertDataHubEntitiesResponse { Successes = [], Failures = r.Entities.Select(e => new DataAccessFailure<JObject>(e, new Exception("write failed"))).ToList() };
                    foreach (var entity in r.Entities) Stored[entity.Value<string>("id")!] = (JObject)entity.DeepClone();
                    return new UpsertDataHubEntitiesResponse { Successes = r.Entities, Failures = [] };
                case DispatchNotificationsRequest r:
                    Notifications.AddRange(r.Notifications);
                    return new DispatchNotificationsResponse { Success = true, Failures = [] };
                default: throw new InvalidOperationException(request.GetType().Name);
            }
        }

        private ChangeTrackingEntry Add(AddTrackedEntityChangeSetRequest r)
        {
            var entry = new ChangeTrackingEntry { id = Guid.NewGuid().ToString(), DataSource = r.DataSource,
                EntityType = r.EntityType, EntityId = r.SourceEntityId, EntryType = ChangeTrackingEntryTypes.Update,
                Data = r.ChangeSet, Timestamp = r.TimeStamp };
            History.Add(entry);
            return entry;
        }
    }

    private sealed class LocalMediator(Func<IRequest, CancellationToken, Task<object>> respond) : IMediator
    {
        public async Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct)
        {
            try { return (TResponse)await respond(request, ct); }
            catch (Exception ex) { return ex; }
        }
        public async Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(IRequest<TResponse> request, CancellationToken ct, Action<Exception>? exceptionHandler = null)
        {
            try { return ((TResponse)await respond(request, ct), null); }
            catch (Exception ex) { return (default, ex); }
        }
        public async Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken ct) => await respond(request, ct);
        public Task<object> SendAndHandleExceptions<TRequest>(TRequest request, CancellationToken ct, Action<Exception>? exceptionHandler = null) where TRequest : IRequest => respond(request, ct);
        public async Task<TResponse> SendAndHandleExceptions<TResponse>(IRequest request, CancellationToken ct, Action<Exception>? exceptionHandler = null) => (TResponse)await respond(request, ct);
    }
}
