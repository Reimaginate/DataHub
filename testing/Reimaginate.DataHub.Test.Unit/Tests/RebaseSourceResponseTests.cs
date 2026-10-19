using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;
using Xunit;
using ClientRequests = Reimaginate.DataHub.SharedModels.Requests.Client;
using Cli = Reimaginate.DataHub.SharedModels.Requests.CLI;
using ClientHandler = Reimaginate.DataHub.Requests.External.Client.RebaseSourceEntities.RebaseSourceEntitiesRequestHandler;
using CliHandler = Reimaginate.DataHub.Requests.External.CLI.RebaseSourceEntities.RebaseSourceEntitiesRequestHandler;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class RebaseSourceResponseTests
{
    private static readonly DateTimeOffset Cutoff = DateTimeOffset.Parse("2026-10-01T00:00:00Z");

    [Theory]
    [InlineData(false, 2, 0)]
    [InlineData(true, 2, 0)]
    [InlineData(false, 2, 1)]
    [InlineData(true, 2, 1)]
    [InlineData(false, 2, 2)]
    [InlineData(true, 2, 2)]
    [InlineData(false, 1, 1)]
    [InlineData(true, 1, 1)]
    [InlineData(false, 0, 0)]
    [InlineData(true, 0, 0)]
    public async Task Overall_status_reflects_entity_results_and_preserves_partial_progress(bool cli, int count, int failedCount)
    {
        var results = Enumerable.Range(0, count).Select(index => new RebaseEntityTrackingResult
        {
            DataSource = "SRC",
            EntityType = "Thing",
            EntityId = $"entity-{index}",
            Success = index >= failedCount,
            FailureReason = index < failedCount ? "Partial rebase: invalid update retained." : null,
            RebasedTo = Cutoff.AddMinutes(index),
            ArchivedTrackingEntries = [new ChangeTrackingEntry { id = $"archived-{index}", Data = new JObject { ["Value"] = index } }]
        }).ToList();
        var mediator = Substitute.For<IMediator>();
        mediator.TrySend(Arg.Any<ProcessRebaseSourceEntitiesRequest>(), Arg.Any<CancellationToken>(), Arg.Any<Action<Exception>?>())
            .Returns(Task.FromResult<(ProcessRebaseSourceEntitiesResponse?, Exception?)>((new() { Results = results }, null)));
        var ids = results.Select(result => result.EntityId).ToList();

        var response = await InvokeAsync(cli, mediator, ids);

        response.Success.Should().Be(failedCount == 0);
        if (failedCount == 0)
            response.FailureReason.Should().BeNull();
        else
            response.FailureReason.Should().Contain($"{failedCount} of {count} source entities failed to rebase");
        response.Results.Should().BeEquivalentTo(results, options => options.WithStrictOrdering());
        await mediator.Received(1).TrySend(
            Arg.Is<ProcessRebaseSourceEntitiesRequest>(request => request != null && request.DataSource == "SRC" &&
                request.EntityType == "Thing" && request.EntityIds.SequenceEqual(ids) && request.RebaseTo == Cutoff),
            TestContext.Current.CancellationToken, Arg.Any<Action<Exception>?>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Request_errors_and_cancellation_still_propagate(bool cli, bool cancelled)
    {
        var mediator = Substitute.For<IMediator>();
        Exception error = cancelled ? new OperationCanceledException() : new InvalidOperationException("tracking read failed");
        mediator.TrySend(Arg.Any<ProcessRebaseSourceEntitiesRequest>(), Arg.Any<CancellationToken>(), Arg.Any<Action<Exception>?>())
            .Returns(Task.FromResult<(ProcessRebaseSourceEntitiesResponse?, Exception?)>((null, error)));

        Func<Task> action = async () => await InvokeAsync(cli, mediator, ["entity-1"]);

        var thrown = await action.Should().ThrowAsync<Exception>();
        thrown.Which.Should().BeSameAs(error);
    }

    private static async Task<(bool Success, string FailureReason, List<RebaseEntityTrackingResult> Results)> InvokeAsync(
        bool cli, IMediator mediator, List<string> ids)
    {
        if (cli)
        {
            var response = await new CliHandler(mediator).HandleAsync(new Cli.RebaseSourceEntitiesRequest
            {
                DataSource = "SRC", EntityType = "Thing", EntityIds = ids, RebaseTo = Cutoff
            }, TestContext.Current.CancellationToken);
            return (response.Success, response.FailureReason, response.Results);
        }

        var clientResponse = await new ClientHandler(mediator).HandleAsync(new ClientRequests.RebaseSourceEntitiesRequest
        {
            DataSource = "SRC", EntityType = "Thing", EntityIds = ids, RebaseTo = Cutoff
        }, TestContext.Current.CancellationToken);
        return (clientResponse.Success, clientResponse.FailureReason, clientResponse.Results);
    }
}
