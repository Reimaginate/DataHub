using FluentAssertions;
using NSubstitute;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;
using Harness = Reimaginate.DataHub.Test.Unit.Tests.RebaseTrackingPersistenceTests.Harness;
using LockService = Reimaginate.ProcessingLockService.ProcessingLockService;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class RebaseLockingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10), Token);
    private static string LockKey(bool source) => source ? "entities/TestSource/Reservation/booking-1" : $"entities/{DataSources.DataHub}/Booking/entity-1";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Every_read_and_write_is_locked_and_501_entities_use_two_separate_batches(bool source)
    {
        using var real = new LockService();
        var service = RebaseBatchLockTests.Forward(real);
        var db = new Harness(source, service);
        for (var id = 2; id <= 501; id++) db.Seed($"entity-{id}");
        var batches = new List<List<string>>();
        var owners = new List<string>();
        service.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(async call =>
            {
                (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty("the preceding batch must release its locks first");
                batches.Add(call.ArgAt<List<string>>(0).ToList());
                owners.Add(call.ArgAt<string>(1));
                call.ArgAt<TimeSpan?>(3).Should().Be(TimeSpan.FromMinutes(5));
                call.ArgAt<TimeSpan?>(4).Should().Be(TimeSpan.FromMinutes(5));
                return await real.WaitForLocksAsync(call.ArgAt<List<string>>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2));
            });
        var creations = 0;
        var deletions = 0;
        var identityBatchSizes = new List<int>();
        db.BeforeRequest = async (request, ct) =>
        {
            if (request is Reimaginate.DataHub.DataAccess.Queries.GetSourceRebaseIdentities.GetSourceRebaseIdentitiesQuery discovery)
            {
                (await real.GetLocksAsync(ct)).Result.Should().BeEmpty("identity discovery follows release of the preceding batch");
                identityBatchSizes.Add(discovery.EntityIds.Count);
                return;
            }
            var held = (await real.GetLocksAsync(ct)).Result!;
            var heldKeys = held.Select(item => item.Key).ToHashSet();
            heldKeys.SetEquals(batches.Last()).Should().BeTrue();
            held.All(item => item.Owner == owners.Last()).Should().BeTrue();
            IEnumerable<string>? touched = request switch
            {
                GetAllTrackingEntriesForEntitiesRequest get => get.EntityIds,
                CommitRebaseCheckpointCommand checkpoint => checkpoint.EntriesToDelete.Select(entry => entry.EntityId),
                CreateCosmosDocumentsCommand<ChangeTrackingEntry> create => create.Documents.Select(entry => entry.EntityId),
                DeleteCosmosDocumentsCommand<ChangeTrackingEntry> delete => delete.Documents.Select(entry => entry.EntityId),
                _ => null
            };
            if (touched != null)
                touched.All(id => heldKeys.Contains($"entities/{(source ? "SRC1" : DataSources.DataHub)}/Thing/{id}")).Should().BeTrue();
            if (request is CommitRebaseCheckpointCommand { Baseline: not null }) Interlocked.Increment(ref creations);
            if (request is CommitRebaseCheckpointCommand) Interlocked.Increment(ref deletions);
        };

        var results = await db.Rebase(sourceDataSource: "src1", sourceEntityType: "thing");

        batches.Select(batch => batch.Count).Should().Equal(500, 1);
        if (source) identityBatchSizes.Should().Equal(500, 1);
        owners.Distinct().Should().ContainSingle();
        Guid.TryParse(owners[0], out _).Should().BeTrue();
        results.Should().HaveCount(501).And.OnlyContain(result => result.Success);
        creations.Should().Be(501);
        deletions.Should().Be(501);
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_batch_does_no_storage_work_and_the_next_batch_still_runs(bool source)
    {
        using var real = new LockService();
        var service = RebaseBatchLockTests.Forward(real);
        var attempts = 0;
        service.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(call => ++attempts == 1
                ? Task.FromResult(new Response<List<ProcessingLock>>(false, failureReason: new FailureReason(FailureShortCodes.InUse)))
                : real.WaitForLocksAsync(call.ArgAt<List<string>>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));
        var db = new Harness(source, service);
        for (var id = 2; id <= 501; id++) db.Seed($"entity-{id}");
        var original = db.History.Take(1000).ToList();
        db.BeforeRequest = (request, _) =>
        {
            if (request is Reimaginate.DataHub.DataAccess.Queries.GetSourceRebaseIdentities.GetSourceRebaseIdentitiesQuery) return Task.CompletedTask;
            attempts.Should().Be(2, "the failed first batch must not read or write storage");
            if (request is GetAllTrackingEntriesForEntitiesRequest get) get.EntityIds.Should().Equal("entity-501");
            return Task.CompletedTask;
        };

        var results = await db.Rebase();

        results.Count(result => !result.Success).Should().Be(500);
        results.Where(result => !result.Success).Should().OnlyContain(result => result.FailureReason.Contains("acquire") && result.ArchivedTrackingEntries.Count == 0);
        results.Single(result => result.EntityId == "entity-501").Success.Should().BeTrue();
        db.History.Where(entry => entry.EntityId != "entity-501").Should().Equal(original);
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_exception_releases_locks_and_preserves_the_original_exception(bool source)
    {
        using var real = new LockService();
        var db = new Harness(source, real);
        var failure = new ApplicationException("read exploded");
        db.BeforeRequest = (_, _) => Task.FromException(failure);

        Func<Task> run = () => db.Rebase();

        (await run.Should().ThrowAsync<ApplicationException>()).Which.Should().BeSameAs(failure);
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
        db.CreateCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Write_exception_still_releases_batch_locks(bool source, bool deletion)
    {
        using var real = new LockService();
        var db = new Harness(source, real) { ThrowOnCreate = !deletion, ThrowOnDelete = deletion };

        (await db.Rebase()).Should().OnlyContain(result => !result.Success);

        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_during_a_read_stops_writes_and_releases_the_batch(bool source)
    {
        using var real = new LockService();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var db = new Harness(source, real);
        db.BeforeRequest = (request, ct) =>
        {
            if (request is GetAllTrackingEntriesForEntitiesRequest)
            {
                cancellation.Cancel();
                ct.ThrowIfCancellationRequested();
            }
            return Task.CompletedTask;
        };

        Func<Task> run = () => db.RebaseWithCancellation(cancellation.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
        db.CreateCalls.Should().Be(0);
        db.DeleteCalls.Should().Be(0);
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overlapping_rebases_serialize_and_use_different_owners(bool source)
    {
        using var real = new LockService();
        var service = RebaseBatchLockTests.Forward(real);
        var first = new Harness(source, service);
        var second = first;
        var entered = Signal();
        var finish = Signal();
        var waiting = Signal();
        var owners = new List<string>();
        service.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(call =>
            {
                owners.Add(call.ArgAt<string>(1));
                var acquired = real.WaitForLocksAsync(call.ArgAt<List<string>>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2));
                if (owners.Count == 2) { acquired.IsCompleted.Should().BeFalse(); waiting.SetResult(); }
                return acquired;
            });
        var reads = 0;
        first.BeforeRequest = async (request, _) =>
        {
            if (request is GetAllTrackingEntriesForEntitiesRequest && Interlocked.Increment(ref reads) == 1)
            { entered.SetResult(); await finish.Task; }
        };
        var firstRun = first.Rebase();
        try
        {
            await Wait(entered.Task);
            var secondRun = second.Rebase(sourceEntityType: "thing", sourceDataSource: "src1");
            await Wait(waiting.Task);
            secondRun.IsCompleted.Should().BeFalse();
            second.CreateCalls.Should().Be(0);
            owners.Distinct().Should().HaveCount(2);
            finish.SetResult();
            await Wait(Task.WhenAll(firstRun, secondRun));
        }
        finally { finish.TrySetResult(); }
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rebase_blocks_an_incoming_merge_of_the_same_entity_but_an_unrelated_merge_finishes(bool source)
    {
        using var real = new LockService();
        var service = RebaseBatchLockTests.Forward(real);
        using var db = new MergeRegressionHarness(false, service) { PerformanceMode = true };
        db.SeedBooking("booking-1", "entity-1");
        db.SeedBooking("booking-2", "entity-2");
        var initialMerge = await db.MergePublicAsync(true, [MergeRegressionHarness.Booked()]);
        initialMerge.Success.Should().BeTrue(initialMerge.FailureReason);
        var entered = Signal();
        var finish = Signal();
        var waiting = Signal();
        db.BeforeRequest = async request =>
        {
            if (request is GetAllTrackingEntriesForEntitiesRequest && !entered.Task.IsCompleted)
            {
                entered.SetResult();
                await finish.Task;
            }
        };
        service.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(call =>
            {
                var keys = call.ArgAt<List<string>>(0);
                var acquired = real.WaitForLocksAsync(keys, call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2));
                if (!Guid.TryParse(call.ArgAt<string>(1), out _) && keys.Contains(LockKey(source)))
                {
                    acquired.IsCompleted.Should().BeFalse();
                    waiting.TrySetResult();
                }
                return acquired;
            });
        var rebasing = db.RebaseAsync(source, sourceEntityType: "reservation", sourceDataSource: "testsource");
        try
        {
            await Wait(entered.Task);
            var merging = db.MergePublicAsync(true, [MergeRegressionHarness.Cancelled()]);
            await Wait(Task.WhenAny(waiting.Task, merging));
            if (merging.IsCompleted) (await merging).Success.Should().BeTrue((await merging).FailureReason);
            await Wait(waiting.Task);
            merging.IsCompleted.Should().BeFalse();
            var unrelated = await db.MergePublicAsync(true, [MergeRegressionHarness.Cancelled("booking-2")]);
            unrelated.Success.Should().BeTrue(unrelated.FailureReason);
            merging.IsCompleted.Should().BeFalse();
            finish.SetResult();
            await Wait(rebasing);
            (await merging).Success.Should().BeTrue((await merging).FailureReason);
        }
        finally { finish.TrySetResult(); }
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(0);
        db.Stored["entity-2"].Value<decimal>("Total").Should().Be(0);
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_incoming_merge_already_holding_the_lock_finishes_before_rebase_reads(bool source)
    {
        using var real = new LockService();
        var service = RebaseBatchLockTests.Forward(real);
        using var db = new MergeRegressionHarness(false, service) { PerformanceMode = true };
        db.SeedBooking("booking-1", "entity-1");
        var entered = Signal();
        var finish = Signal();
        var waiting = Signal();
        db.BeforeRequest = async request =>
        {
            var sourceRead = request is GetAllTrackingEntriesForEntitiesRequest or GetTrackingEntriesQuery;
            var hubRead = request is Reimaginate.DataHub.SharedModels.Requests.Client.GetDataHubEntitiesByIdRequest;
            if ((source ? sourceRead : hubRead) && !entered.Task.IsCompleted)
            {
                entered.SetResult();
                await finish.Task;
            }
        };
        service.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(call =>
            {
                var keys = call.ArgAt<List<string>>(0);
                var acquired = real.WaitForLocksAsync(keys, call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2));
                if (Guid.TryParse(call.ArgAt<string>(1), out _))
                {
                    keys.Should().Contain(LockKey(source));
                    acquired.IsCompleted.Should().BeFalse();
                    waiting.SetResult();
                }
                return acquired;
            });
        var merging = db.MergePublicAsync(true, [MergeRegressionHarness.Cancelled()]);
        try
        {
            await Wait(entered.Task);
            var rebasing = db.RebaseAsync(source, sourceEntityType: "reservation", sourceDataSource: "testsource");
            await Wait(waiting.Task);
            rebasing.IsCompleted.Should().BeFalse();
            finish.SetResult();
            (await merging).Success.Should().BeTrue((await merging).FailureReason);
            await Wait(rebasing);
            (await rebasing).Should().OnlyContain(result => result.Success);
        }
        finally { finish.TrySetResult(); }
        var replay = Reimaginate.DataHub.Helpers.ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(
            source ? "TestSource" : DataSources.DataHub, source ? "Reservation" : "Booking", source ? "booking-1" : "entity-1"));
        replay.Value<decimal>("Total").Should().Be(0);
    }
}
