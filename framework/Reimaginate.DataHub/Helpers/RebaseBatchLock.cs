using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Helpers;

internal static class RebaseBatchLock
{
    internal static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan RenewalInterval = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan LeaseSafetyMargin = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan RenewalTimeout = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(30);

    // A non-null return value means acquisition failed and the batch was never run.
    public static async Task<string> ExecuteAsync(IProcessingLockService service, List<string> keys, string owner,
        Func<CancellationToken, Action, Task> processBatch, CancellationToken cancellationToken, TimeProvider timeProvider = null)
    {
        var clock = timeProvider ?? TimeProvider.System;
        cancellationToken.ThrowIfCancellationRequested();
        var acquisition = await service.WaitForLocksAsync(keys, owner, cancellationToken,
            duration: LeaseDuration, waitTimeOut: TimeSpan.FromMinutes(5));
        if (acquisition?.Success != true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Failed responses may contain the *blocking owner's* locks, not ours.
            return $"Failed to acquire rebase batch locks: {acquisition?.FailureReason?.ShortCode}: {acquisition?.FailureReason?.Details}";
        }

        var locks = acquisition.Result ?? [];
        // Unregister links without synchronously waiting for arbitrary backend callbacks.
        // Linked CTS disposal can otherwise defeat the cleanup deadlines.
        using var batchCancellation = new CancellationTokenSource();
        using var renewalCancellation = new CancellationTokenSource();
        var requestLink = cancellationToken.Register(() => RequestCancellation(batchCancellation));
        var batchLink = batchCancellation.Token.Register(() => RequestCancellation(renewalCancellation));
        LeaseGuard lease = null;
        Exception error = null;
        Task renewal = Task.CompletedTask;

        async Task RenewAsync()
        {
            try
            {
                using var timer = new PeriodicTimer(RenewalInterval, clock);
                while (await timer.WaitForNextTickAsync(renewalCancellation.Token))
                {
                    lease.ThrowIfLost();
                    var response = await BoundedAsync(service.RenewLocksAsync(locks, renewalCancellation.Token, LeaseDuration),
                        RenewalTimeout, clock, renewalCancellation.Token);
                    if (renewalCancellation.IsCancellationRequested) return;
                    var renewed = response?.Result;
                    if (response?.Success != true || renewed == null || renewed.Count != locks.Count ||
                        renewed.Any(result => result?.Success != true || result.Result == null) ||
                        !renewed.Select(result => (result.Result.Key, result.Result.Token)).ToHashSet()
                            .SetEquals(locks.Select(item => (item.Key, item.Token))))
                        throw new InvalidOperationException("Failed to renew rebase batch locks; the batch was stopped.");
                    lease.Confirm(renewed.Select(result => result.Result).ToList());
                }
            }
            catch (OperationCanceledException) when (renewalCancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                lease.Fail(new InvalidOperationException("Failed to renew rebase batch locks; the batch was stopped.", ex));
            }
        }

        void RecordCleanupError(string key, Exception failure)
        {
            if (error == null) error = failure;
            else error.Data[key] = failure;
        }

        try
        {
            if (locks.Count != keys.Count || locks.Any(item => item == null || item.Owner != owner || string.IsNullOrEmpty(item.Token)) ||
                !locks.Select(item => item.Key).ToHashSet().SetEquals(keys))
                throw new InvalidOperationException("Acquisition did not confirm every rebase batch lock.");

            lease = new LeaseGuard(clock, batchCancellation);
            lease.Confirm(locks);
            renewal = RenewAsync();
            lease.ThrowIfLost();
            batchCancellation.Token.ThrowIfCancellationRequested();
            await processBatch(batchCancellation.Token, lease.ThrowIfLost);
            lease.ThrowIfLost();
            batchCancellation.Token.ThrowIfCancellationRequested();
        }
        catch (Exception ex) { error = ex; }
        finally
        {
            lease?.Dispose();
            try { await BoundedAsync(renewalCancellation.CancelAsync(), ShutdownTimeout, clock, CancellationToken.None); }
            catch (Exception ex) { RecordCleanupError("RebaseRenewalStopFailure", ex); }
            try { await BoundedAsync(renewal, ShutdownTimeout, clock, CancellationToken.None); }
            catch (Exception ex) { RecordCleanupError("RebaseRenewalShutdownFailure", ex); }
            var leaseError = lease?.Failure;
            if (leaseError != null)
            {
                if (error == null || (error is OperationCanceledException && !cancellationToken.IsCancellationRequested))
                    error = leaseError;
                else if (!ReferenceEquals(error, leaseError))
                    error.Data["RebaseLockRenewalFailure"] = leaseError;
            }

            try
            {
                // Token checks make a late renewal/release harmless after ownership changes.
                var ownedLocks = locks.Where(item => item != null && item.Owner == owner &&
                    !string.IsNullOrEmpty(item.Token) && keys.Contains(item.Key)).ToList();
                if (ownedLocks.Count != 0)
                {
                    using var releaseCancellation = new CancellationTokenSource(ReleaseTimeout, clock);
                    Response<List<Response<Null>>> release;
                    try
                    {
                        release = await BoundedAsync(service.ReleaseLocksAsync(ownedLocks, releaseCancellation.Token),
                            ReleaseTimeout, clock, releaseCancellation.Token);
                    }
                    catch (OperationCanceledException ex) when (releaseCancellation.IsCancellationRequested)
                    {
                        throw new TimeoutException("Timed out releasing rebase batch locks.", ex);
                    }
                    if (release?.Success != true || release.Result == null || release.Result.Count != ownedLocks.Count ||
                        release.Result.Any(result => result?.Success != true))
                        throw new InvalidOperationException("Failed to release rebase batch locks.");
                }
            }
            catch (Exception ex) { RecordCleanupError("RebaseLockReleaseFailure", ex); }
            requestLink.Unregister();
            batchLink.Unregister();
        }

        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
        return null;
    }

    private static async Task<T> BoundedAsync<T>(Task<T> task, TimeSpan timeout, TimeProvider clock, CancellationToken token)
    {
        try { return await task.WaitAsync(timeout, clock, token); }
        catch { _ = ObserveAsync(task); throw; }
    }

    private static async Task BoundedAsync(Task task, TimeSpan timeout, TimeProvider clock, CancellationToken token)
    {
        try { await task.WaitAsync(timeout, clock, token); }
        catch { _ = ObserveAsync(task); throw; }
    }

    // Repositories may ignore cancellation. Observe late faults without waiting indefinitely.
    private static async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch { }
    }

    private static void RequestCancellation(CancellationTokenSource source)
    {
        try { _ = ObserveAsync(source.CancelAsync()); }
        catch (ObjectDisposedException) { } // A callback can race with unregister during cleanup.
    }

    private sealed class LeaseGuard : IDisposable
    {
        private readonly object gate = new();
        private readonly TimeProvider clock;
        private readonly CancellationTokenSource batchCancellation;
        private readonly ITimer deadline;
        private long confirmedAt;
        private TimeSpan validFor;
        private bool confirmed;
        private bool stopped;
        private Exception failure;

        public LeaseGuard(TimeProvider clock, CancellationTokenSource batchCancellation)
        {
            this.clock = clock;
            this.batchCancellation = batchCancellation;
            deadline = clock.CreateTimer(_ => CheckDeadline(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public Exception Failure { get { lock (gate) return failure; } }

        public void Confirm(List<ProcessingLock> locks)
        {
            lock (gate)
            {
                if (stopped) return;
                // A late success must never revive a lease that has already become unsafe.
                CheckExpiry();
                if (failure == null)
                {
                    var remaining = locks.Min(item => item.Timestamp + item.Duration) - clock.GetUtcNow();
                    validFor = (remaining < LeaseDuration ? remaining : LeaseDuration) - LeaseSafetyMargin;
                    confirmedAt = clock.GetTimestamp();
                    confirmed = true;
                    if (validFor <= TimeSpan.Zero) failure = Expired();
                    else deadline.Change(validFor, Timeout.InfiniteTimeSpan);
                }
            }
            ThrowIfLost();
        }

        public void ThrowIfLost()
        {
            Exception expired;
            lock (gate)
            {
                CheckExpiry();
                expired = failure;
                if (expired != null && !stopped) _ = ObserveAsync(batchCancellation.CancelAsync());
            }
            if (expired != null)
            {
                ExceptionDispatchInfo.Capture(expired).Throw();
            }
        }

        public void Fail(Exception error)
        {
            lock (gate)
            {
                if (stopped) return;
                failure ??= error;
                _ = ObserveAsync(batchCancellation.CancelAsync());
            }
        }

        private void CheckDeadline()
        {
            lock (gate)
            {
                if (stopped) return;
                CheckExpiry();
                if (failure == null)
                {
                    // A timer can fire slightly early because of clock/timer precision.
                    var remaining = validFor - clock.GetElapsedTime(confirmedAt);
                    deadline.Change(remaining > TimeSpan.FromMilliseconds(1) ? remaining : TimeSpan.FromMilliseconds(1),
                        Timeout.InfiniteTimeSpan);
                    return;
                }
                _ = ObserveAsync(batchCancellation.CancelAsync());
            }
        }

        private void CheckExpiry()
        {
            if (!stopped && confirmed && clock.GetElapsedTime(confirmedAt) >= validFor)
                failure ??= Expired();
        }

        private static Exception Expired() => new InvalidOperationException(
            "Rebase batch lock lease expired before renewal was confirmed; the batch was stopped.");

        public void Dispose()
        {
            lock (gate)
            {
                stopped = true;
                deadline.Dispose();
            }
        }
    }
}
