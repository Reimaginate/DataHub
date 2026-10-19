using FluentAssertions;
using NSubstitute;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;
using Reimaginate.DataHub.DataAccess.Queries.GetSourceRebaseIdentities;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Xunit;
using LockService = Reimaginate.ProcessingLockService.ProcessingLockService;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class RebaseTrackingReadCancellationTests
{
    [Fact]
    public async Task Every_page_and_id_batch_receives_the_read_token_and_retains_query_parameters()
    {
        using var cancellation = new CancellationTokenSource();
        var reads = new List<GetTrackingEntriesQuery>();
        var mediator = ReaderMediator((query, ct) =>
        {
            ct.Should().Be(cancellation.Token);
            reads.Add(query);
            var more = reads.Count % 2 == 1;
            return Task.FromResult(new PagedResults<ChangeTrackingEntry>
            {
                Results = [new ChangeTrackingEntry { id = $"entry-{reads.Count}" }],
                MoreResultsAvailable = more, ContinuationToken = more ? $"page-{reads.Count + 1}" : null
            });
        });
        var ids = Enumerable.Range(1, 5001).Select(i => $"entity-{i}").ToList();

        var response = await new GetAllTrackingEntriesForEntitiesRequestHandler(mediator).HandleAsync(new()
        {
            DataSource = "SRC", EntityType = "Thing", EntityIds = ids, ObserveCancellation = true
        }, cancellation.Token);

        response.TrackingEntries.Select(entry => entry.id).Should().Equal("entry-1", "entry-2", "entry-3", "entry-4");
        reads.Should().HaveCount(4).And.OnlyContain(query => query.DataSource == "SRC" && query.EntityType == "Thing" && query.PageSize == 5000);
        reads.Select(query => query.EntityIds.Count).Should().Equal(5000, 5000, 1, 1);
        reads.Select(query => query.ContinuationToken).Should().Equal(null, "page-2", null, "page-4");
        ids.Should().HaveCount(5001);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Already_cancelled_rebase_reads_do_not_query_even_when_no_ids_are_supplied(bool empty)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reads = 0;
        var mediator = ReaderMediator((_, _) => { reads++; return Task.FromResult(Page()); });

        Func<Task> run = () => new GetAllTrackingEntriesForEntitiesRequestHandler(mediator).HandleAsync(new()
        {
            EntityIds = empty ? [] : ["entity-1"], ObserveCancellation = true
        }, cancellation.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
        reads.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_after_a_response_stops_before_the_next_page_or_id_batch(bool morePages)
    {
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        var mediator = ReaderMediator((_, _) =>
        {
            reads++;
            cancellation.Cancel(); // Simulate a read that returns despite cancellation.
            return Task.FromResult(Page(morePages));
        });

        Func<Task> run = () => new GetAllTrackingEntriesForEntitiesRequestHandler(mediator).HandleAsync(new()
        {
            EntityIds = Enumerable.Range(1, 5001).Select(i => $"entity-{i}").ToList(), ObserveCancellation = true
        }, cancellation.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
        reads.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Default_read_behavior_remains_uncancelled_for_existing_callers(bool initiallyCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        if (initiallyCancelled) cancellation.Cancel();
        var reads = 0;
        var mediator = ReaderMediator((_, ct) =>
        {
            ct.Should().Be(CancellationToken.None);
            reads++;
            cancellation.Cancel();
            return Task.FromResult(Page(more: reads == 1));
        });

        var response = await new GetAllTrackingEntriesForEntitiesRequestHandler(mediator).HandleAsync(new()
        {
            DataSource = "SRC", EntityType = "Thing", EntityIds = ["entity-1"]
        }, cancellation.Token);

        reads.Should().Be(2);
        response.TrackingEntries.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Cancelling_a_real_rebase_tracking_read_stops_before_writes_and_releases_locks(bool source, bool continuationPage)
    {
        using var locks = new LockService();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var mediator = ReaderMediator(async (_, ct) =>
        {
            ct.CanBeCanceled.Should().BeTrue();
            if (++reads == 1 && continuationPage) return Page(more: true);
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Page();
        });
        var reader = new GetAllTrackingEntriesForEntitiesRequestHandler(mediator);
        mediator.TrySend(Arg.Any<GetSourceRebaseIdentitiesQuery>(), Arg.Any<CancellationToken>(), Arg.Any<Action<Exception>?>())
            .Returns(Task.FromResult<(List<SourceRebaseIdentity>?, Exception?)>(([new("SRC", "Thing")], null)));
        mediator.TrySend(Arg.Any<GetAllTrackingEntriesForEntitiesRequest>(), Arg.Any<CancellationToken>(), Arg.Any<Action<Exception>?>())
            .Returns(async call =>
            {
                var request = call.Arg<GetAllTrackingEntriesForEntitiesRequest>()!;
                request.ObserveCancellation.Should().BeTrue();
                try { return (await reader.HandleAsync(request, call.Arg<CancellationToken>()), (Exception?)null); }
                catch (Exception ex) { return ((GetAllTrackingEntriesForEntitiesResponse?)null, ex); }
            });

        Task run = source
            ? new ProcessRebaseSourceEntitiesRequestHandler(mediator, locks).HandleAsync(new()
            {
                DataSource = "SRC", EntityType = "Thing", EntityIds = ["entity-1"]
            }, cancellation.Token)
            : new ProcessRebaseDataHubEntitiesRequestHandler(mediator, locks).HandleAsync(new()
            {
                EntityType = "Thing", EntityIds = ["entity-1"]
            }, cancellation.Token);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            (await locks.GetLocksAsync(TestContext.Current.CancellationToken)).Result.Should().ContainSingle();
            cancellation.Cancel();
            Func<Task> completed = () => run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await completed.Should().ThrowAsync<OperationCanceledException>();
            reads.Should().Be(continuationPage ? 2 : 1);
            mediator.ReceivedCalls().Select(call => call.GetArguments()[0]).All(request => request is
                GetAllTrackingEntriesForEntitiesRequest or GetTrackingEntriesQuery or GetSourceRebaseIdentitiesQuery).Should().BeTrue();
            (await locks.GetLocksAsync(TestContext.Current.CancellationToken)).Result.Should().BeEmpty();
        }
        finally
        {
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken); } catch (OperationCanceledException) { }
        }
    }

    private static PagedResults<ChangeTrackingEntry> Page(bool more = false) => new()
    {
        Results = [new ChangeTrackingEntry { id = "entry-1" }], MoreResultsAvailable = more,
        ContinuationToken = more ? "page-2" : null
    };

    private static IMediator ReaderMediator(Func<GetTrackingEntriesQuery, CancellationToken, Task<PagedResults<ChangeTrackingEntry>>> read)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.TrySend(Arg.Any<GetTrackingEntriesQuery>(), Arg.Any<CancellationToken>(), Arg.Any<Action<Exception>?>())
            .Returns(async call =>
            {
                try { return (await read(call.Arg<GetTrackingEntriesQuery>()!, call.Arg<CancellationToken>()), (Exception?)null); }
                catch (Exception ex) { return ((PagedResults<ChangeTrackingEntry>?)null, ex); }
            });
        return mediator;
    }
}
