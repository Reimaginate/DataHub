using FluentAssertions;
using NSubstitute;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class EntityConfigServiceTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 15)]
    public async Task Cached_results_expire_and_new_policy_is_discovered(bool initiallyPresent, int minutes)
    {
        var clock = new TestClock();
        var calls = 0;
        var mediator = Mediator((_, _) =>
        {
            calls++;
            return Task.FromResult(Page(calls == 1 && !initiallyPresent ? null :
                new EntityConfig { EntityType = "Booking", DoNotTrack = calls > 1 }));
        });
        using var service = new EntityConfigService(mediator, clock);

        var first = await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true);
        clock.Advance(TimeSpan.FromMinutes(minutes) - TimeSpan.FromSeconds(1));
        (await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true)).Should().BeSameAs(first);
        calls.Should().Be(1);
        clock.Advance(TimeSpan.FromSeconds(2));
        (await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true)).DoNotTrack.Should().BeTrue();
        calls.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_waiters_share_one_fetch_and_one_cancellation_does_not_cancel_others(bool missing)
    {
        var completion = new TaskCompletionSource<PagedResults<EntityConfig>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        CancellationToken fetchToken = default;
        var mediator = Mediator((_, ct) => { Interlocked.Increment(ref calls); fetchToken = ct; return completion.Task; });
        using var service = new EntityConfigService(mediator);
        using var cancelled = new CancellationTokenSource();
        var first = service.GetEntityConfig("Booking", cancelled.Token);
        var others = Enumerable.Range(0, 50).Select(_ => service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true)).ToArray();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        fetchToken.IsCancellationRequested.Should().BeFalse();
        completion.SetResult(Page(missing ? null : new EntityConfig { EntityType = "Booking", DoNotTrack = true }));
        var results = await Task.WhenAll(others);
        calls.Should().Be(1);
        results.Should().OnlyContain(c => missing ? c == null : c!.DoNotTrack == true);
    }

    [Fact]
    public async Task Different_types_load_independently()
    {
        var booking = new TaskCompletionSource<PagedResults<EntityConfig>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mediator = Mediator((q, _) => q.Parameters.Single().Value!.ToString() == "Booking"
            ? booking.Task : Task.FromResult(Page(new EntityConfig { EntityType = "Performance" })));
        using var service = new EntityConfigService(mediator);
        var pending = service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true);
        (await service.GetEntityConfig("Performance", TestContext.Current.CancellationToken)).EntityType.Should().Be("Performance");
        pending.IsCompleted.Should().BeFalse();
        booking.SetResult(Page(new EntityConfig { EntityType = "Booking", DoNotTrack = true }));
        (await pending).DoNotTrack.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_cancelled_fetch_is_not_cached(bool cancellation)
    {
        var calls = 0;
        var mediator = Mediator((_, _) => ++calls == 1
            ? Task.FromException<PagedResults<EntityConfig>>(cancellation ? new OperationCanceledException() : new InvalidOperationException("offline"))
            : Task.FromResult(Page(new EntityConfig { EntityType = "Booking", DoNotTrack = true })));
        using var service = new EntityConfigService(mediator);
        await Assert.ThrowsAnyAsync<Exception>(() => service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true));
        (await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true)).DoNotTrack.Should().BeTrue();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task Already_cancelled_caller_does_not_start_fetch()
    {
        var mediator = Mediator((_, _) => throw new InvalidOperationException("must not query"));
        using var service = new EntityConfigService(mediator);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetEntityConfig("Booking", new CancellationToken(true)));
        mediator.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_configuration_on_later_page_fails_and_can_be_retried()
    {
        var calls = 0;
        var mediator = Mediator((q, _) =>
        {
            calls++;
            return Task.FromResult(calls == 1
                ? new PagedResults<EntityConfig> { Results = [new() { EntityType = "Booking" }], MoreResultsAvailable = true, ContinuationToken = "next" }
                : Page(new EntityConfig { EntityType = "Booking", DoNotTrack = true }));
        });
        using var service = new EntityConfigService(mediator);
        await Assert.ThrowsAsync<Exception>(() => service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true));
        (await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true)).DoNotTrack.Should().BeTrue();
        calls.Should().Be(3);
    }

    [Fact]
    public async Task Mediator_error_response_is_not_treated_as_missing_configuration()
    {
        var mediator = Substitute.For<IMediator>();
        var calls = 0;
        mediator.TrySend(Arg.Any<GetCosmosDocumentsQuery<EntityConfig>>(), Arg.Any<CancellationToken>(), Arg.Any<Action<Exception>?>())
            .Returns(_ => ++calls == 1
                ? ((PagedResults<EntityConfig>?)null, (Exception?)new InvalidOperationException("database unavailable"))
                : (Page(new EntityConfig { EntityType = "Booking", DoNotTrack = true }), (Exception?)null));
        using var service = new EntityConfigService(mediator);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true));
        (await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true)).DoNotTrack.Should().BeTrue();
        calls.Should().Be(2);
    }
    [Fact]
    public async Task Legacy_merge_read_discovers_new_configuration_even_after_tracking_cached_a_miss()
    {
        var calls = 0;
        var mediator = Mediator((_, _) => Task.FromResult(Page(++calls == 1 ? null :
            new EntityConfig { EntityType = "Booking", DoNotTrack = true })));
        using var service = new EntityConfigService(mediator);
        (await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true)).Should().BeNull();
        (await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true)).Should().BeNull();
        calls.Should().Be(1);
        (await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken)).DoNotTrack.Should().BeTrue();
        calls.Should().Be(2);
        (await service.GetEntityConfig("Booking", TestContext.Current.CancellationToken, cacheMissing: true)).DoNotTrack.Should().BeTrue();
        calls.Should().Be(2);
    }
    private static PagedResults<EntityConfig> Page(EntityConfig? config) => new() { Results = config == null ? [] : [config] };

    private static IMediator Mediator(Func<GetCosmosDocumentsQuery<EntityConfig>, CancellationToken, Task<PagedResults<EntityConfig>>> query)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.TrySend(Arg.Any<GetCosmosDocumentsQuery<EntityConfig>>(), Arg.Any<CancellationToken>(), Arg.Any<Action<Exception>?>())
            .Returns(async call => ((PagedResults<EntityConfig>?)await query(call.Arg<GetCosmosDocumentsQuery<EntityConfig>>()!, call.Arg<CancellationToken>()), (Exception?)null));
        return mediator;
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan elapsed) => now += elapsed;
    }
}
