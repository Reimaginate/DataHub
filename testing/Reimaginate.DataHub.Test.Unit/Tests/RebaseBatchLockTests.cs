using FluentAssertions;
using NSubstitute;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;
using LockService = Reimaginate.ProcessingLockService.ProcessingLockService;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class RebaseBatchLockTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10), Token);

    [Fact]
    public async Task Failed_acquisition_does_not_run_the_batch_or_release_blocking_locks()
    {
        using var real = new LockService();
        var blocker = (await real.WaitForLocksAsync(["entity"], "other", Token)).Result!;
        var service = Substitute.For<IProcessingLockService>();
        service.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(new Response<List<ProcessingLock>>(false, blocker, new FailureReason(FailureShortCodes.InUse)));
        var ran = false;

        var failure = await RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase", (_, _) => { ran = true; return Task.CompletedTask; }, Token);

        failure.Should().Contain("Failed to acquire");
        ran.Should().BeFalse();
        service.ReceivedCalls().Should().ContainSingle();
        (await real.GetLocksAsync(Token)).Result!.Should().ContainSingle(l => l.Owner == "other");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_always_releases_successfully_acquired_locks(bool cancelInAcquisition)
    {
        using var real = new LockService();
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var service = Forward(real);
        if (cancelInAcquisition)
            service.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
                .Returns(async call =>
                {
                    var result = await real.WaitForLocksAsync(call.ArgAt<List<string>>(0), call.ArgAt<string>(1), Token);
                    cancelled.Cancel();
                    return result;
                });

        var run = () => RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase", (ct, _) =>
        {
            cancelled.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, cancelled.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
        await service.Received(1).ReleaseLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Is<CancellationToken>(ct => ct.CanBeCanceled));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Release_failure_is_surfaced_without_replacing_an_existing_error(bool batchFails)
    {
        using var real = new LockService();
        var service = Forward(real);
        service.ReleaseLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>())
            .Returns(new Response<List<Response<Null>>>(false, failureReason: new FailureReason(FailureShortCodes.UnexpectedFailure)));
        var original = new ApplicationException("batch failed");

        var run = () => RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase",
            (_, _) => batchFails ? Task.FromException(original) : Task.CompletedTask, Token);

        if (batchFails)
        {
            var error = (await run.Should().ThrowAsync<ApplicationException>()).Which;
            error.Should().BeSameAs(original);
            error.Data["RebaseLockReleaseFailure"].Should().BeOfType<InvalidOperationException>();
        }
        else
            await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to release*");
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("throws")]
    [InlineData("missing")]
    [InlineData("wrong-token")]
    public async Task Renewal_failure_cancels_the_batch_and_releases_locks(string failure)
    {
        using var real = new LockService();
        var service = Forward(real);
        var time = new TickTimeProvider();
        service.RenewLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(call => failure switch
            {
                "throws" => Task.FromException<Response<List<Response<ProcessingLock>>>>(new ApplicationException("renewal exploded")),
                "missing" => Task.FromResult(new Response<List<Response<ProcessingLock>>>(true, [])),
                "wrong-token" => Task.FromResult(new Response<List<Response<ProcessingLock>>>(true,
                    [new(true, new ProcessingLock { Key = "entity", Token = "different-token" })])),
                _ => Task.FromResult(new Response<List<Response<ProcessingLock>>>(false, failureReason: new FailureReason(FailureShortCodes.InUse)))
            });
        var started = Signal();
        var cancelled = false;
        var run = RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase", async (ct, _) =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { cancelled = true; throw; }
        }, Token, time);
        await Wait(started.Task);

        time.Tick();

        Func<Task> finish = () => Wait(run);
        await finish.Should().ThrowAsync<Exception>().Where(ex => !typeof(OperationCanceledException).IsAssignableFrom(ex.GetType()));
        cancelled.Should().BeTrue();
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
        time.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Successful_renewal_retains_ownership_and_stops_before_release()
    {
        using var real = new LockService();
        var service = Forward(real);
        var time = new TickTimeProvider();
        var renewed = Signal();
        service.RenewLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(async call =>
            {
                var result = await real.RenewLocksAsync(call.ArgAt<IList<ProcessingLock>>(0), call.ArgAt<CancellationToken>(1), call.ArgAt<TimeSpan?>(2));
                foreach (var item in result.Result!) item.Result!.Timestamp = time.GetUtcNow();
                renewed.SetResult();
                return result;
            });
        var finish = Signal();
        var run = RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase", (_, _) => finish.Task, Token, time);

        time.Tick();
        await Wait(renewed.Task);
        (await real.GetLocksAsync(Token)).Result!.Should().ContainSingle(l => l.Owner == "rebase");
        time.Due.Should().Be(TimeSpan.FromMinutes(1));
        time.Period.Should().Be(TimeSpan.FromMinutes(1));
        finish.SetResult();
        await Wait(run);

        time.Disposed.Should().BeTrue();
        time.Tick();
        await service.Received(1).RenewLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>(), TimeSpan.FromMinutes(5));
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Fact]
    public async Task Cleanup_does_not_wait_for_a_repository_ignoring_cancellation_and_late_renewal_cannot_recreate_locks()
    {
        using var real = new LockService();
        var service = Forward(real);
        var time = new TickTimeProvider();
        var renewing = Signal();
        var allowRenewal = Signal();
        Task<Response<List<Response<ProcessingLock>>>>? pending = null;
        service.RenewLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(call => pending = RenewLate(call.ArgAt<IList<ProcessingLock>>(0)));
        async Task<Response<List<Response<ProcessingLock>>>> RenewLate(IList<ProcessingLock> locks)
        {
            renewing.SetResult();
            await allowRenewal.Task;
            return await real.RenewLocksAsync(locks, Token, TimeSpan.FromMinutes(5));
        }
        var finish = Signal();
        var run = RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase", (_, _) => finish.Task, Token, time);
        time.Tick();
        await Wait(renewing.Task);

        finish.SetResult();
        await Wait(run);
        pending!.IsCompleted.Should().BeFalse();
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
        var other = await real.WaitForLocksAsync(["entity"], "other", Token);
        other.Success.Should().BeTrue();
        allowRenewal.SetResult();
        await Wait(pending);
        (await pending).Success.Should().BeFalse();
        (await real.GetLocksAsync(Token)).Result!.Should().ContainSingle(item => item.Owner == "other");
    }

    [Fact]
    public async Task Hung_renewal_times_out_and_cancels_the_batch_before_the_lease_expires()
    {
        using var real = new LockService();
        var service = Forward(real);
        var time = new TickTimeProvider();
        var pending = new TaskCompletionSource<Response<List<Response<ProcessingLock>>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.RenewLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>()).Returns(pending.Task);
        var run = RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase",
            (ct, _) => Task.Delay(Timeout.InfiniteTimeSpan, ct), Token, time);
        time.Tick();
        await Wait(time.WhenScheduled(RebaseBatchLock.RenewalTimeout));

        time.Advance(RebaseBatchLock.RenewalTimeout);

        Func<Task> finish = () => Wait(run);
        var error = (await finish.Should().ThrowAsync<InvalidOperationException>()).Which;
        error.InnerException.Should().BeOfType<TimeoutException>();
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
        pending.SetException(new ApplicationException("late backend failure"));
    }

    [Fact]
    public async Task Independent_deadline_cancels_even_when_the_periodic_renewal_timer_is_delayed()
    {
        using var real = new LockService();
        var time = new TickTimeProvider();
        var run = RebaseBatchLock.ExecuteAsync(real, ["entity"], "rebase",
            (ct, _) => Task.Delay(Timeout.InfiniteTimeSpan, ct), Token, time);

        time.Advance(RebaseBatchLock.LeaseDuration, firePeriodic: false);

        Func<Task> finish = () => Wait(run);
        await finish.Should().ThrowAsync<InvalidOperationException>().WithMessage("*lease expired*");
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Fact]
    public async Task Direct_guard_blocks_writes_even_when_all_timer_callbacks_are_delayed()
    {
        using var real = new LockService();
        var time = new TickTimeProvider();
        var proceed = Signal();
        var writes = 0;
        var run = RebaseBatchLock.ExecuteAsync(real, ["entity"], "rebase", async (ct, ensureLease) =>
        {
            await proceed.Task;
            ct.IsCancellationRequested.Should().BeFalse("the watchdog callback has deliberately not run");
            ensureLease();
            writes++;
        }, Token, time);

        time.Advance(RebaseBatchLock.LeaseDuration, fireTimers: false);
        proceed.SetResult();

        Func<Task> finish = () => Wait(run);
        await finish.Should().ThrowAsync<InvalidOperationException>().WithMessage("*lease expired*");
        writes.Should().Be(0);
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Fact]
    public async Task Late_success_cannot_revive_an_expired_lease()
    {
        using var real = new LockService();
        var service = Forward(real);
        var time = new TickTimeProvider();
        var pending = new TaskCompletionSource<Response<List<Response<ProcessingLock>>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        IList<ProcessingLock>? renewing = null;
        service.RenewLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(call => { renewing = call.ArgAt<IList<ProcessingLock>>(0); return pending.Task; });
        var run = RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase",
            (ct, _) => Task.Delay(Timeout.InfiniteTimeSpan, ct), Token, time);
        time.Tick();
        await Wait(time.WhenScheduled(RebaseBatchLock.RenewalTimeout));
        time.Advance(RebaseBatchLock.LeaseDuration, fireTimers: false);
        pending.SetResult(new Response<List<Response<ProcessingLock>>>(true, renewing!.Select(item =>
            new Response<ProcessingLock>(true, new ProcessingLock(item.Key, time.GetUtcNow(), RebaseBatchLock.LeaseDuration, item.Owner, item.TenantId, item.Token))).ToList()));

        Func<Task> finish = () => Wait(run);
        await finish.Should().ThrowAsync<InvalidOperationException>().WithMessage("*lease expired*");
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hung_release_times_out_independently_of_the_request_and_preserves_the_original_error(bool batchFails)
    {
        using var real = new LockService();
        var service = Forward(real);
        var time = new TickTimeProvider();
        var releasing = Signal();
        var pending = new TaskCompletionSource<Response<List<Response<Null>>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken releaseToken = default;
        service.ReleaseLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>())
            .Returns(call => { releaseToken = call.ArgAt<CancellationToken>(1); releasing.SetResult(); return pending.Task; });
        var original = new ApplicationException("original batch error");
        var run = RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase",
            (_, _) => batchFails ? Task.FromException(original) : Task.CompletedTask, Token, time);
        await Wait(releasing.Task);
        releaseToken.IsCancellationRequested.Should().BeFalse();

        time.Advance(RebaseBatchLock.ReleaseTimeout);

        Func<Task> finish = () => Wait(run);
        if (batchFails)
        {
            var error = (await finish.Should().ThrowAsync<ApplicationException>()).Which;
            error.Should().BeSameAs(original);
            error.Data["RebaseLockReleaseFailure"].Should().BeOfType<TimeoutException>();
        }
        else await finish.Should().ThrowAsync<TimeoutException>();
        releaseToken.IsCancellationRequested.Should().BeTrue();
        pending.SetException(new ApplicationException("late release failure"));
    }

    [Fact]
    public async Task Persistence_checks_cancellation_before_starting_a_write()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var mediator = Substitute.For<Reimaginate.Mediator.IMediator>();

        Func<Task> run = () => RebaseTrackingPersistence.PersistAsync(mediator, [], cancelled.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
        mediator.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Persistence_checks_the_lease_before_atomic_checkpoint_writes(int expireAtCheck)
    {
        using var real = new LockService();
        var time = new TickTimeProvider();
        var mediator = Substitute.For<IMediator>();
        var baseline = new InitTrackedEntityRequest { DataSource = "SRC", EntityType = "Thing", EntityId = "entity", Timestamp = time.GetUtcNow(), EntityData = new Newtonsoft.Json.Linq.JObject { ["Value"] = 1 } };
        var plan = new RebaseTrackingPlan(baseline, [new ChangeTrackingEntry { id = "old-entry", Timestamp = baseline.Timestamp.Value }],
            new RebaseEntityTrackingResult { ArchivedTrackingEntries = [] });
        var checks = 0;

        Func<Task> run = () => RebaseBatchLock.ExecuteAsync(real, ["entity"], "rebase", (ct, ensureLease) =>
            RebaseTrackingPersistence.PersistAsync(mediator, [plan], ct, () =>
            {
                if (++checks == expireAtCheck) time.Advance(RebaseBatchLock.LeaseDuration, fireTimers: false);
                ensureLease();
            }), Token, time);

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*lease expired*");
        mediator.ReceivedCalls().Should().BeEmpty();
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Fact]
    public async Task A_confirmed_renewal_extends_the_deadline()
    {
        using var real = new LockService();
        var service = Forward(real);
        var time = new TickTimeProvider();
        service.RenewLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(async call =>
            {
                var response = await real.RenewLocksAsync(call.ArgAt<IList<ProcessingLock>>(0), Token, RebaseBatchLock.LeaseDuration);
                foreach (var item in response.Result!) item.Result!.Timestamp = time.GetUtcNow();
                return response;
            });
        var proceed = Signal();
        var run = RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase", async (ct, ensureLease) =>
        {
            await proceed.Task;
            ensureLease();
            ct.ThrowIfCancellationRequested();
        }, Token, time);
        time.Tick();
        await Wait(time.WhenScheduled(RebaseBatchLock.LeaseDuration - RebaseBatchLock.LeaseSafetyMargin, 2));

        // Cross the original deadline without running further periodic renewals.
        time.Advance(TimeSpan.FromMinutes(3.5), firePeriodic: false);
        proceed.SetResult();

        await Wait(run);
        (await run).Should().BeNull();
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Fact]
    public async Task An_acquisition_response_already_past_its_safe_deadline_never_starts_the_batch()
    {
        using var real = new LockService();
        var service = Forward(real);
        var time = new TickTimeProvider();
        service.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(async call =>
            {
                var response = await real.WaitForLocksAsync(call.ArgAt<List<string>>(0), call.ArgAt<string>(1), Token);
                foreach (var item in response.Result!) item.Timestamp = time.GetUtcNow() - RebaseBatchLock.LeaseDuration;
                return response;
            });
        var ran = false;
        Func<Task> run = () => RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase",
            (_, _) => { ran = true; return Task.CompletedTask; }, Token, time);

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*lease expired*");
        ran.Should().BeFalse();
        (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
    }

    [Fact]
    public async Task A_stalled_renewal_cancellation_callback_cannot_prevent_cleanup_from_attempting_release()
    {
        using var real = new LockService();
        var service = Forward(real);
        var time = new TickTimeProvider();
        var callbackStarted = Signal();
        var finishCallback = Signal();
        var callbackFinished = Signal();
        var pending = new TaskCompletionSource<Response<List<Response<ProcessingLock>>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.RenewLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(call =>
            {
                call.ArgAt<CancellationToken>(1).Register(() =>
                {
                    callbackStarted.SetResult();
                    finishCallback.Task.GetAwaiter().GetResult();
                    callbackFinished.SetResult();
                });
                return pending.Task;
            });
        var finishBatch = Signal();
        var run = RebaseBatchLock.ExecuteAsync(service, ["entity"], "rebase", (_, _) => finishBatch.Task, Token, time);
        time.Tick();
        await Wait(time.WhenScheduled(RebaseBatchLock.RenewalTimeout));
        try
        {
            finishBatch.SetResult();
            await Wait(callbackStarted.Task);
            await Wait(time.WhenScheduled(RebaseBatchLock.ShutdownTimeout));
            time.Advance(RebaseBatchLock.ShutdownTimeout);

            Func<Task> finish = () => Wait(run);
            await finish.Should().ThrowAsync<TimeoutException>();
            (await real.GetLocksAsync(Token)).Result!.Should().BeEmpty();
        }
        finally
        {
            finishCallback.TrySetResult();
            pending.TrySetResult(new Response<List<Response<ProcessingLock>>>(false));
            await Wait(callbackFinished.Task);
        }
    }

    internal static IProcessingLockService Forward(IProcessingLockService real)
    {
        var service = Substitute.For<IProcessingLockService>();
        service.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(call => real.WaitForLocksAsync(call.ArgAt<List<string>>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2), call.ArgAt<TimeSpan?>(3), call.ArgAt<TimeSpan?>(4)));
        service.RenewLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(call => real.RenewLocksAsync(call.ArgAt<IList<ProcessingLock>>(0), call.ArgAt<CancellationToken>(1), call.ArgAt<TimeSpan?>(2)));
        service.ReleaseLocksAsync(Arg.Any<IList<ProcessingLock>>(), Arg.Any<CancellationToken>())
            .Returns(call => real.ReleaseLocksAsync(call.ArgAt<IList<ProcessingLock>>(0), call.ArgAt<CancellationToken>(1)));
        return service;
    }

    // Virtual UTC and monotonic time, with independently scheduled timers.
    private sealed class TickTimeProvider : TimeProvider
    {
        private readonly object gate = new();
        private readonly DateTimeOffset start = DateTimeOffset.UtcNow;
        private readonly List<ManualTimer> timers = [];
        private readonly Dictionary<(TimeSpan Due, int Occurrence), TaskCompletionSource> scheduled = [];
        private readonly Dictionary<TimeSpan, int> scheduleCounts = [];
        private long elapsed;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() { lock (gate) return elapsed; }
        public override DateTimeOffset GetUtcNow() { lock (gate) return start.AddTicks(elapsed); }
        public bool Disposed { get { lock (gate) return timers.Where(t => t.Period == RebaseBatchLock.RenewalInterval).All(t => t.Disposed); } }
        public TimeSpan Due { get { lock (gate) return timers.Single(t => t.Period > TimeSpan.Zero).InitialDue; } }
        public TimeSpan Period { get { lock (gate) return timers.Single(t => t.Period > TimeSpan.Zero).Period; } }
        public Task WhenScheduled(TimeSpan due, int occurrence = 1)
        {
            lock (gate)
            {
                var key = (due, occurrence);
                if (!scheduled.TryGetValue(key, out var signal)) scheduled[key] = signal = Signal();
                return signal.Task;
            }
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (gate)
            {
                var timer = new ManualTimer(this, callback, state, dueTime);
                timers.Add(timer);
                timer.Change(dueTime, period);
                return timer;
            }
        }
        public void Tick() => Advance(Reimaginate.DataHub.Helpers.RebaseBatchLock.RenewalInterval);
        public void Advance(TimeSpan by, bool fireTimers = true, bool firePeriodic = true)
        {
            List<ManualTimer> ready;
            lock (gate)
            {
                elapsed += by.Ticks;
                ready = fireTimers ? timers.Where(t => !t.Disposed && t.Next <= elapsed &&
                    (firePeriodic || t.Period == Timeout.InfiniteTimeSpan)).OrderBy(t => t.Next).ToList() : [];
                foreach (var timer in ready)
                    timer.Next = timer.Period == Timeout.InfiniteTimeSpan ? long.MaxValue : elapsed + timer.Period.Ticks;
            }
            foreach (var timer in ready) if (!timer.Disposed) timer.Callback(timer.State);
        }
        private sealed class ManualTimer(TickTimeProvider provider, TimerCallback callback, object? state, TimeSpan initialDue) : ITimer
        {
            public TimeSpan InitialDue { get; } = initialDue;
            public TimerCallback Callback { get; } = callback;
            public object? State { get; } = state;
            public TimeSpan Period { get; private set; }
            public long Next { get; set; }
            public bool Disposed { get; private set; }
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (provider.gate)
                {
                    if (Disposed) return false;
                    Next = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : provider.elapsed + dueTime.Ticks;
                    Period = period;
                    var count = provider.scheduleCounts.GetValueOrDefault(dueTime) + 1;
                    provider.scheduleCounts[dueTime] = count;
                    var key = (dueTime, count);
                    if (!provider.scheduled.TryGetValue(key, out var signal)) provider.scheduled[key] = signal = Signal();
                    signal.TrySetResult();
                    return true;
                }
            }
            public void Dispose() { lock (provider.gate) Disposed = true; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
