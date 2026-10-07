using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OneOf;
using Reimaginate.DataHub.Requests.External.Client.MergeEntities;
using Reimaginate.DataHub.Requests.External.Client.MergeUntrackedEntities;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.Requests.Internal.MergeExistingEntities;
using Reimaginate.DataHub.Requests.Internal.MergeExistingUntrackedEntities;
using Reimaginate.DataHub.Requests.Internal.MergeNewEntities;
using Reimaginate.DataHub.Requests.Internal.RecordSyncEventsAgainstDataHubEntities;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class MergeEntityReferenceFailureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Failed_lookup_prevents_all_merges_preserves_reason_and_releases_source_locks(bool tracked, bool partialResults)
    {
        var resolution = new ResolveEntityReferencesResponse
        {
            Success = false,
            FailureReason = "Reference lookup timed out.",
            Results = partialResults ? [Resolved("source-1", "existing-1")] : []
        };
        var harness = new MergeHarness(resolution);

        var response = await harness.HandleAsync(tracked, [Merge("source-1"), Merge("source-2")]);

        response.Success.Should().BeFalse();
        response.FailureReason.Should().Be(resolution.FailureReason);
        response.Results.Should().BeEmpty();
        harness.Mediator.Requests.Should().ContainSingle().Which.Should().BeOfType<ResolveEntityReferencesRequest>();
        await harness.AssertSourceLocksReleased();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, " \t")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, " \t")]
    public async Task Failed_lookup_without_a_reason_returns_useful_fallback(bool tracked, string? reason)
    {
        var harness = new MergeHarness(new ResolveEntityReferencesResponse { Success = false, FailureReason = reason });

        var response = await harness.HandleAsync(tracked, [Merge("source-1")]);

        response.Success.Should().BeFalse();
        response.FailureReason.Should().Be("Entity reference resolution failed.");
        harness.Mediator.Requests.Should().ContainSingle().Which.Should().BeOfType<ResolveEntityReferencesRequest>();
        await harness.AssertSourceLocksReleased();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Successful_lookup_without_matches_still_creates_entities(bool tracked)
    {
        var harness = new MergeHarness(new ResolveEntityReferencesResponse { Success = true });

        var response = await harness.HandleAsync(tracked, [Merge("source-1")]);

        response.Success.Should().BeTrue();
        response.Results.Should().ContainSingle().Which.MergeOutcome.Should().Be(MergeOutcomes.NewEntityCreated);
        harness.Mediator.Requests.OfType<MergeNewEntitiesRequest>().Should().ContainSingle()
            .Which.MergeRequests.Select(request => request.SourceEntityId).Should().Equal("source-1");
        harness.ExistingMergeRequests.Should().BeEmpty();
        await harness.AssertSourceLocksReleased();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Successful_lookup_with_a_match_updates_the_existing_entity(bool tracked)
    {
        var harness = new MergeHarness(new ResolveEntityReferencesResponse
        {
            Success = true,
            Results = [Resolved("source-1", "existing-1")]
        });

        var response = await harness.HandleAsync(tracked, [Merge("source-1")]);

        response.Success.Should().BeTrue();
        var result = response.Results.Should().ContainSingle().Subject;
        result.MergeOutcome.Should().Be(MergeOutcomes.EntityMatchedAndUpdated);
        result.DataHubEntityId.Should().Be("existing-1");
        harness.Mediator.Requests.OfType<MergeNewEntitiesRequest>().Should().BeEmpty();
        harness.ExistingMergeRequests.Should().ContainSingle().Which.SourceEntityId.Should().Be("source-1");
        await harness.AssertSourceLocksReleased();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Individual_duplicate_failure_excludes_only_that_record_and_other_records_continue(bool tracked)
    {
        var failure = new ResolveEntityReferenceException(Resolved("ambiguous", "duplicate-1").SourceEntityReference);
        var harness = new MergeHarness(new ResolveEntityReferencesResponse
        {
            Success = true,
            Results = [Resolved("ambiguous", "duplicate-1"), Resolved("existing", "existing-1")],
            ResolutionFailures = [failure]
        });

        var response = await harness.HandleAsync(tracked, [Merge("ambiguous"), Merge("new"), Merge("existing")]);

        response.Success.Should().BeTrue();
        response.Results.Should().HaveCount(3);
        var failedRecord = response.Results.Single(result => result.SourceEntityId == "ambiguous");
        failedRecord.MergeOutcome.Should().Be(MergeOutcomes.MergeFailed);
        failedRecord.FailureReason.Should().Be(failure.Message);
        response.Results.Single(result => result.SourceEntityId == "new").MergeOutcome.Should().Be(MergeOutcomes.NewEntityCreated);
        response.Results.Single(result => result.SourceEntityId == "existing").DataHubEntityId.Should().Be("existing-1");
        harness.Mediator.Requests.OfType<MergeNewEntitiesRequest>().Should().ContainSingle()
            .Which.MergeRequests.Select(request => request.SourceEntityId).Should().Equal("new");
        harness.ExistingMergeRequests.Should().ContainSingle().Which.SourceEntityId.Should().Be("existing");
        await harness.AssertSourceLocksReleased();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Later_group_lookup_failure_stops_processing_without_replaying_completed_groups(bool tracked)
    {
        var harness = new MergeHarness(
            new ResolveEntityReferencesResponse { Success = true },
            new ResolveEntityReferencesResponse { Success = false, FailureReason = "Second group lookup failed." },
            new ResolveEntityReferencesResponse { Success = true });

        var response = await harness.HandleAsync(tracked,
            [Merge("first", "Booking"), Merge("second", "Contact"), Merge("third", "Performance")]);

        response.Success.Should().BeFalse();
        response.FailureReason.Should().Be("Second group lookup failed.");
        harness.Mediator.Requests.OfType<ResolveEntityReferencesRequest>().Should().HaveCount(2);
        harness.Mediator.Requests.OfType<MergeNewEntitiesRequest>().Should().ContainSingle()
            .Which.MergeRequests.Select(request => request.SourceEntityId).Should().Equal("first");
        harness.ExistingMergeRequests.Should().BeEmpty();
        await harness.AssertSourceLocksReleased();
    }

    private static MergeEntityRequest Merge(string sourceId, string entityType = "Booking") => new()
    {
        DataSource = "Ticketure",
        SourceEntityType = "TicketAudit",
        SourceEntityId = sourceId,
        DataHubEntityType = entityType,
        Data = new JObject()
    };

    private static ResolvedEntityReference Resolved(string sourceId, string entityId) => new()
    {
        SourceEntityReference = new ExternalEntityReference
        {
            DataSource = "Ticketure", SourceEntityType = "TicketAudit", EntityType = "Booking", EntityId = sourceId
        },
        DataHubEntityReference = new EntityReference { EntityType = "Booking", EntityId = entityId }
    };

    private sealed class MergeHarness
    {
        private readonly IEntityConfigService _config = Substitute.For<IEntityConfigService>();
        private readonly IProcessingLockService _locks = Substitute.For<IProcessingLockService>();
        private readonly List<ProcessingLock> _sourceLocks = [new()];
        private readonly List<ProcessingLock> _entityLocks = [new()];

        public RecordingMediator Mediator { get; }
        public IEnumerable<MergeEntityRequest> ExistingMergeRequests => Mediator.Requests.SelectMany(request => request switch
        {
            MergeExistingEntitiesRequest existing => existing.MergeRequests,
            MergeExistingUntrackedEntitiesRequest existing => existing.MergeRequests,
            _ => Enumerable.Empty<MergeEntityRequest>()
        });

        public MergeHarness(params ResolveEntityReferencesResponse[] resolutions)
        {
            var pendingResolutions = new Queue<ResolveEntityReferencesResponse>(resolutions);
            Mediator = new RecordingMediator(request => request switch
            {
                ResolveEntityReferencesRequest => pendingResolutions.Dequeue(),
                MergeNewEntitiesRequest merge => new MergeNewEntitiesResponse
                {
                    Successes = merge.MergeRequests.Select(item => Result(item, $"new-{item.SourceEntityId}", MergeOutcomes.NewEntityCreated)).ToList()
                },
                MergeExistingEntitiesRequest merge => new MergeExistingEntitiesResponse
                {
                    Results = ExistingResults(merge.MergeRequests, merge.ResolvedDataHubEntities)
                },
                MergeExistingUntrackedEntitiesRequest merge => new MergeExistingUntrackedEntitiesResponse
                {
                    Results = ExistingResults(merge.MergeRequests, merge.ResolvedDataHubEntities)
                },
                DispatchNotificationsRequest => new DispatchNotificationsResponse { Success = true },
                LogSyncEventsRequest<MergeSuccess> => new LogEventsResponse { Success = true },
                LogSyncEventsRequest<MergeFailure> => new LogEventsResponse { Success = true },
                RecordSyncEventsAgainstDataHubEntitiesRequest<MergeFailure> => new NullResponse(),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
            });
            _config.GetEntityConfig(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new EntityConfig());
            _locks.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
                .Returns(call => Task.FromResult(new Response<List<ProcessingLock>>(true,
                    call.Arg<List<string>>()!.All(id => id.StartsWith("entities/Ticketure/", StringComparison.Ordinal)) ? _sourceLocks : _entityLocks, null)));
        }

        public Task<MergeEntitiesResponse> HandleAsync(bool tracked, List<MergeEntityRequest> requests) => tracked
            ? new MergeEntitiesRequestHandler(Mediator, _config, _locks).HandleAsync(
                new MergeEntitiesRequest { CorrelationId = "test-merge", Requests = requests }, CancellationToken.None)
            : new MergeUntrackedEntitiesRequestHandler(Mediator, _config, _locks).HandleAsync(
                new MergeUntrackedEntitiesRequest { CorrelationId = "test-merge", Requests = requests }, CancellationToken.None);

        public async Task AssertSourceLocksReleased() =>
            await _locks.Received(1).ReleaseLocksAsync(Arg.Is<List<ProcessingLock>>(locks => ReferenceEquals(locks, _sourceLocks)), CancellationToken.None);

        private static List<MergeEntityResult> ExistingResults(IEnumerable<MergeEntityRequest> requests, List<ResolvedEntityReference> references) =>
            requests.Select(request => Result(request,
                references.Single(reference => reference.SourceEntityReference.EntityId == request.SourceEntityId).DataHubEntityReference.EntityId,
                MergeOutcomes.EntityMatchedAndUpdated)).ToList();

        private static MergeEntityResult Result(MergeEntityRequest request, string entityId, string outcome) => new()
        {
            DataSource = request.DataSource,
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = request.SourceEntityId,
            DataHubEntityType = request.DataHubEntityType,
            DataHubEntityId = entityId,
            MergeOutcome = outcome,
            ResultingDataHubEntity = new JObject { ["id"] = entityId, ["lastUpdated"] = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero) }
        };
    }

    private sealed class RecordingMediator(Func<IRequest, object> responseFactory) : IMediator
    {
        public List<IRequest> Requests { get; } = [];

        public Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(OneOf<TResponse, Exception>.FromT0((TResponse)responseFactory(request)));
        }

        public Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(IRequest<TResponse> request,
            CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            Requests.Add(request);
            return Task.FromResult(((TResponse?)responseFactory(request), (Exception?)null));
        }

        public Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<object> SendAndHandleExceptions<TRequest>(TRequest request, CancellationToken cancellationToken,
            Action<Exception>? exceptionHandler = null) where TRequest : IRequest => throw new NotSupportedException();
        public Task<TResponse> SendAndHandleExceptions<TResponse>(IRequest request, CancellationToken cancellationToken,
            Action<Exception>? exceptionHandler = null) => throw new NotSupportedException();
    }
}
