using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OneOf;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;
using LockService = Reimaginate.ProcessingLockService.ProcessingLockService;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class PatchLockingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10), Token);

    [Theory]
    [InlineData("single", DataSources.DataHub)]
    [InlineData("single", "Source")]
    [InlineData("batch", DataSources.DataHub)]
    [InlineData("batch", "Source")]
    [InlineData("standalone", DataSources.DataHub)]
    [InlineData("standalone", "Source")]
    public async Task Locks_exclude_competitors_until_all_persistence_completes(string path, string source)
    {
        using var db = new Harness(source, path == "batch" ? 2 : 1);
        var tracking = db.TrackingWrite = new Pause();
        var entity = db.EntityWrite = source == DataSources.DataHub ? new Pause() : null;
        var patch = db.Patch(path);
        try
        {
            await Wait(tracking.Entered.Task);
            await db.AssertLocked();
            db.TrackingWrites.Should().Be(0);
            tracking.Continue.TrySetResult();

            if (entity != null)
            {
                await Wait(entity.Entered.Task);
                await db.AssertLocked();
                db.TrackingWrites.Should().Be(db.Items.Count);
                db.EntityWrites.Should().Be(0);
                entity.Continue.TrySetResult();
            }

            await Wait(patch);
            (await patch).Should().BeTrue();
            db.TrackingWrites.Should().Be(db.Items.Count);
            db.EntityWrites.Should().Be(source == DataSources.DataHub ? db.Items.Count : 0);
            await db.AssertReleasedAndReacquirable();
        }
        finally
        {
            tracking.Continue.TrySetResult();
            entity?.Continue.TrySetResult();
            await Wait(patch);
        }
    }

    [Fact]
    public async Task Standalone_dry_run_still_acquires_and_releases_its_own_lock()
    {
        using var db = new Harness(DataSources.DataHub);
        db.Items[0].CommitToDb = false;
        var read = db.EntityRead = new Pause();
        var patch = db.Patch("standalone");
        try
        {
            await Wait(read.Entered.Task);
            await db.AssertLocked();
            read.Continue.TrySetResult();
            await Wait(patch);
            (await patch).Should().BeTrue();
            db.TrackingWrites.Should().Be(0);
            db.EntityWrites.Should().Be(0);
            await db.AssertReleasedAndReacquirable();
        }
        finally
        {
            read.Continue.TrySetResult();
            await Wait(patch);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persistence_exception_releases_locks_and_batch_request_reuse_acquires_a_lock(bool batch)
    {
        using var db = new Harness(DataSources.DataHub) { ThrowOnEntityWrite = true };
        if (batch)
        {
            Func<Task> run = () => db.Patch("internal-batch");
            await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("write failed");
        }
        else
        {
            (await db.Patch("standalone")).Should().BeFalse();
        }
        await db.AssertReleasedAndReacquirable();

        // Reuse the actual batch item after failure. A stale ownership marker would
        // make this standalone call skip acquisition, even though the batch has ended.
        db.Items[0].Cache.Clear();
        db.Items[0].CommitToDb = false;
        var read = db.EntityRead = new Pause();
        var retry = db.Patch("standalone");
        try
        {
            await Wait(read.Entered.Task);
            await db.AssertLocked();
            read.Continue.TrySetResult();
            await Wait(retry);
            (await retry).Should().BeTrue();
            await db.AssertReleasedAndReacquirable();
        }
        finally
        {
            read.Continue.TrySetResult();
            await Wait(retry);
        }
    }

    private sealed class Pause
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task Reach(CancellationToken ct)
        {
            Entered.TrySetResult();
            await Continue.Task.WaitAsync(ct);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly LockService locks = new();
        private readonly LocalMediator mediator;
        private readonly ProcessPatchEntityRequestHandler single;
        private readonly ProcessPatchEntitiesRequestHandler batch;
        private readonly List<JObject> stored;
        public List<ProcessPatchEntityRequest> Items { get; }
        public Pause? TrackingWrite { get; set; }
        public Pause? EntityWrite { get; set; }
        public Pause? EntityRead { get; set; }
        public bool ThrowOnEntityWrite { get; init; }
        public int TrackingWrites { get; private set; }
        public int EntityWrites { get; private set; }

        public Harness(string source, int count = 1)
        {
            Items = Enumerable.Range(1, count).Select(id => new ProcessPatchEntityRequest
            {
                RequestId = $"request-{id}", CorrelationId = "patch-owner", DataSource = source,
                EntityType = "Thing", EntityId = $"entity-{id}",
                Operations = [new Patch { Operation = "set", Path = "Name", Value = "Updated" }]
            }).ToList();
            stored = Items.Select(item => new JObject
            {
                ["id"] = item.EntityId, ["entityType"] = item.EntityType, ["Name"] = "Original",
                ["lastUpdated"] = DateTimeOffset.Parse("2030-01-01T00:00:00Z")
            }).ToList();
            mediator = new LocalMediator(Respond);
            var time = Substitute.For<ITimeService>();
            time.Now().Returns(DateTimeOffset.Parse("2030-01-02T00:00:00Z"));
            var configs = Substitute.For<IEntityConfigService>();
            single = new(mediator, locks, time, configs);
            batch = new(mediator, locks, time, configs);
        }

        private List<string> Keys => Items.Select(item => $"entities/{item.DataSource}/{item.EntityType}/{item.EntityId}").ToList();

        public async Task AssertLocked()
        {
            var held = (await locks.GetLocksAsync(Token)).Result!;
            held.Select(item => item.Key).Should().BeEquivalentTo(Keys);
            var attempt = await locks.GetLocksAsync(Keys, "competitor", requireAll: true, cancellationToken: Token);
            attempt.Success.Should().BeFalse("persistence must exclude a different lock owner");
            attempt.FailureReason!.ShortCode.Should().Be(FailureShortCodes.InUse);
        }

        public async Task AssertReleasedAndReacquirable()
        {
            (await locks.GetLocksAsync(Token)).Result.Should().BeEmpty();
            var acquired = await locks.GetLocksAsync(Keys, "competitor", requireAll: true, cancellationToken: Token);
            acquired.Success.Should().BeTrue();
            (await locks.ReleaseLocksAsync(acquired.Result!, Token)).Success.Should().BeTrue();
        }

        public async Task<bool> Patch(string path)
        {
            if (path == "standalone") return (await single.HandleAsync(Items[0], Token)).Success;
            if (path == "internal-batch")
                return (await batch.HandleAsync(new ProcessPatchEntitiesRequest { CorrelationId = "patch-owner", Requests = Items }, Token)).Results.All(result => result.Success);
            var requests = Items.Select(item => new PatchEntityRequest
            {
                CorrelationId = item.CorrelationId, DataSource = item.DataSource, EntityType = item.EntityType,
                EntityId = item.EntityId, Operations = item.Operations
            }).ToList();
            if (path == "single")
                return (await new Reimaginate.DataHub.Requests.External.Client.PatchEntity.PatchEntityRequestHandler(mediator)
                    .HandleAsync(requests[0], Token)).Success;
            return (await new Reimaginate.DataHub.Requests.External.Client.PatchEntities.PatchEntitiesRequestHandler(mediator)
                .HandleAsync(new PatchEntitiesRequest { CorrelationId = "patch-owner", Requests = requests }, Token)).Success;
        }

        private async Task<object> Respond(IRequest request, CancellationToken ct)
        {
            switch (request)
            {
                case ProcessPatchEntitiesRequest r: return await batch.HandleAsync(r, ct);
                case ProcessPatchEntityRequest r: return await single.HandleAsync(r, ct);
                case GetMaterializedEntitiesByIdRequest r:
                    if (EntityRead != null) await EntityRead.Reach(ct);
                    return new GetMaterializedEntitiesByIdResponse { Results = stored.Where(e => r.EntityIds.Contains(e.Value<string>("id")!)).Select(e => (JObject)e.DeepClone()).ToList() };
                case GetTrackedEntityRequest r: return stored.Single(e => e.Value<string>("id") == r.EntityId).DeepClone();
                case GetAllTrackingEntriesForEntitiesRequest r:
                    return new GetAllTrackingEntriesForEntitiesResponse
                    {
                        TrackingEntries = r.EntityIds.Select(id => new ChangeTrackingEntry { DataSource = r.DataSource, EntityType = r.EntityType, EntityId = id }).ToList()
                    };
                case AddTrackedEntityChangeSetsRequest r:
                    if (TrackingWrite != null) await TrackingWrite.Reach(ct);
                    TrackingWrites += r.Requests.Count;
                    return new AddTrackedEntityChangeSetsResponse { Successes = [], Failures = [] };
                case AddTrackedEntityChangeSetRequest:
                    if (TrackingWrite != null) await TrackingWrite.Reach(ct);
                    TrackingWrites++;
                    return new ChangeTrackingEntry();
                case UpsertDataHubEntitiesCommand r:
                    if (EntityWrite != null) await EntityWrite.Reach(ct);
                    if (ThrowOnEntityWrite) throw new InvalidOperationException("write failed");
                    EntityWrites += r.Entities.Count;
                    return new UpsertDataHubEntitiesResponse { Successes = r.Entities, Failures = [] };
                default: throw new InvalidOperationException(request.GetType().Name);
            }
        }

        public void Dispose() => locks.Dispose();
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
