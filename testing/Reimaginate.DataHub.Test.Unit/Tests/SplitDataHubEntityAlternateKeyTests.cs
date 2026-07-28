using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OneOf;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.External.CLI.SplitDataHubEntityAlternateKey;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Requests.Internal.ProcessSplitDataHubEntityAlternateKey;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class SplitDataHubEntityAlternateKeyTests
{
    [Fact]
    public async Task Cli_handler_should_forward_request_to_internal_processor()
    {
        var mediator = new RecordingMediator(_ => new ProcessSplitDataHubEntityAlternateKeyResponse
        {
            Success = true,
            OriginalEntityId = "entity-1",
            NewEntityId = "entity-2"
        });
        var handler = new SplitDataHubEntityAlternateKeyRequestHandler(mediator);

        var response = await handler.HandleAsync(new SplitDataHubEntityAlternateKeyRequest
        {
            CorrelationId = "corr-1",
            EntityType = "Contact",
            EntityId = "entity-1",
            Key = "src1.contact",
            Value = "source-1",
            Silent = true,
            DryRun = true
        }, CancellationToken.None);

        response.Success.Should().BeTrue();
        var processRequest = mediator.Requests.Should().ContainSingle().Subject.Should().BeOfType<ProcessSplitDataHubEntityAlternateKeyRequest>().Subject;
        processRequest.CorrelationId.Should().Be("corr-1");
        processRequest.EntityType.Should().Be("Contact");
        processRequest.EntityId.Should().Be("entity-1");
        processRequest.Key.Should().Be("src1.contact");
        processRequest.Value.Should().Be("source-1");
        processRequest.Silent.Should().BeTrue();
        processRequest.DryRun.Should().BeTrue();
    }

    [Fact]
    public async Task Process_should_dry_run_split_without_writes()
    {
        var scenario = CreateScenario();
        var mediator = new RecordingMediator(request => request switch
        {
            GetMaterializedEntitiesByIdRequest => new GetMaterializedEntitiesByIdResponse { Results = [scenario.Entity] },
            GetAllTrackingEntriesForEntitiesRequest => new GetAllTrackingEntriesForEntitiesResponse { TrackingEntries = scenario.TrackingEntries },
            UpsertDataHubEntitiesCommand => throw new InvalidOperationException("Dry-run should not upsert entities."),
            UpsertCosmosDocumentsCommand<ChangeTrackingEntry> => throw new InvalidOperationException("Dry-run should not upsert tracking."),
            _ => throw new InvalidOperationException(request.GetType().FullName)
        });
        var handler = CreateHandler(mediator);

        var response = await handler.HandleAsync(Request(dryRun: true), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Changed.Should().BeTrue();
        response.NewEntityId.Should().Be("new-entity");
        response.CopiedTrackingEntries.Should().Be(2);
        mediator.Requests.Should().NotContain(request => request is UpsertDataHubEntitiesCommand);
        mediator.Requests.Should().NotContain(request => request is UpsertCosmosDocumentsCommand<ChangeTrackingEntry>);
    }

    [Fact]
    public async Task Process_should_split_entity_and_rewrite_cloned_tracking()
    {
        var scenario = CreateScenario();
        List<JObject>? upsertedEntities = null;
        List<ChangeTrackingEntry>? upsertedTracking = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetMaterializedEntitiesByIdRequest => new GetMaterializedEntitiesByIdResponse { Results = [scenario.Entity] },
            GetAllTrackingEntriesForEntitiesRequest => new GetAllTrackingEntriesForEntitiesResponse { TrackingEntries = scenario.TrackingEntries },
            UpsertDataHubEntitiesCommand upsert => Capture(upsert.Entities, ref upsertedEntities, new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] }),
            UpsertCosmosDocumentsCommand<ChangeTrackingEntry> upsert => Capture(upsert.Documents, ref upsertedTracking, new UpsertCosmosDocumentsResponse<ChangeTrackingEntry> { Successes = upsert.Documents, Failures = [] }),
            _ => throw new InvalidOperationException(request.GetType().FullName)
        });
        var handler = CreateHandler(mediator);

        var response = await handler.HandleAsync(Request(), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.CopiedTrackingEntries.Should().Be(2);

        upsertedEntities.Should().NotBeNull();
        var original = upsertedEntities!.Single(entity => entity.Value<string>(nameof(DataHubEntity.id)) == "entity-1");
        var split = upsertedEntities!.Single(entity => entity.Value<string>(nameof(DataHubEntity.id)) == "new-entity");
        AlternateKeys(original).Should().ContainSingle().Which.Should().Match<AlternateKey>(key => key.Key == "src2.contact" && key.Value == "source-1");
        AlternateKeys(split).Should().ContainSingle().Which.Should().Match<AlternateKey>(key => key.Key == "src1.contact" && key.Value == "source-1");

        upsertedTracking.Should().NotBeNull();
        var clonedTracking = upsertedTracking!
            .Where(entry => entry.EntityId == "new-entity")
            .ToList();
        clonedTracking.Should().HaveCount(2);
        clonedTracking.Select(entry => entry.id).Should().OnlyContain(id => !string.IsNullOrWhiteSpace(id));

        var reassembled = ChangeTrackingHelper.ReassembleEntity(clonedTracking);
        reassembled.Value<string>(nameof(DataHubEntity.id)).Should().Be("new-entity");
        reassembled.Value<string>("Name").Should().Be("Current");
        AlternateKeys(reassembled).Should().ContainSingle().Which.Should().Match<AlternateKey>(key => key.Key == "src1.contact" && key.Value == "source-1");

        var originalTrackingUpdate = upsertedTracking!.Single(entry => entry.EntityId == "entity-1");
        originalTrackingUpdate.EntryType.Should().Be(ChangeTrackingEntryTypes.Update);
        originalTrackingUpdate.Data.Should().ContainKey(nameof(DataHubEntity.alternateKeys));
    }

    [Fact]
    public async Task Process_should_return_not_found_when_entity_is_missing()
    {
        var mediator = new RecordingMediator(_ => new GetMaterializedEntitiesByIdResponse { Results = [] });
        var handler = CreateHandler(mediator);

        var response = await handler.HandleAsync(Request(dryRun: true), CancellationToken.None);

        response.Success.Should().BeFalse();
        response.FailureReason.Should().Be("ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task Process_should_fail_when_target_key_is_missing()
    {
        var scenario = CreateScenario();
        var mediator = new RecordingMediator(request => request switch
        {
            GetMaterializedEntitiesByIdRequest => new GetMaterializedEntitiesByIdResponse { Results = [scenario.Entity] },
            _ => throw new InvalidOperationException(request.GetType().FullName)
        });
        var handler = CreateHandler(mediator);

        var response = await handler.HandleAsync(Request(key: "missing.contact", dryRun: true), CancellationToken.None);

        response.Success.Should().BeFalse();
        response.FailureReason.Should().Be("ALTERNATE_KEY_NOT_FOUND");
    }

    [Fact]
    public async Task Process_should_fail_when_target_key_value_is_not_duplicated()
    {
        var entity = Entity(("src1.contact", "source-1"), ("src2.contact", "source-2"));
        var mediator = new RecordingMediator(request => request switch
        {
            GetMaterializedEntitiesByIdRequest => new GetMaterializedEntitiesByIdResponse { Results = [entity] },
            _ => throw new InvalidOperationException(request.GetType().FullName)
        });
        var handler = CreateHandler(mediator);

        var response = await handler.HandleAsync(Request(dryRun: true), CancellationToken.None);

        response.Success.Should().BeFalse();
        response.FailureReason.Should().Be("ALTERNATE_KEY_VALUE_NOT_DUPLICATED");
    }

    [Fact]
    public async Task Process_should_suppress_original_tracking_update_when_silent()
    {
        var scenario = CreateScenario();
        List<JObject>? upsertedEntities = null;
        List<ChangeTrackingEntry>? upsertedTracking = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetMaterializedEntitiesByIdRequest => new GetMaterializedEntitiesByIdResponse { Results = [scenario.Entity] },
            GetAllTrackingEntriesForEntitiesRequest => new GetAllTrackingEntriesForEntitiesResponse { TrackingEntries = scenario.TrackingEntries },
            UpsertDataHubEntitiesCommand upsert => Capture(upsert.Entities, ref upsertedEntities, new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] }),
            UpsertCosmosDocumentsCommand<ChangeTrackingEntry> upsert => Capture(upsert.Documents, ref upsertedTracking, new UpsertCosmosDocumentsResponse<ChangeTrackingEntry> { Successes = upsert.Documents, Failures = [] }),
            _ => throw new InvalidOperationException(request.GetType().FullName)
        });
        var handler = CreateHandler(mediator);

        var response = await handler.HandleAsync(Request(silent: true), CancellationToken.None);

        response.Success.Should().BeTrue();
        var original = upsertedEntities!.Single(entity => entity.Value<string>(nameof(DataHubEntity.id)) == "entity-1");
        original.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated)).Should().Be(DateTimeOffset.Parse("2026-06-07T10:00:00+10:00"));
        upsertedTracking!.Should().OnlyContain(entry => entry.EntityId == "new-entity");
        upsertedTracking.Should().HaveCount(2);
    }

    private static ProcessSplitDataHubEntityAlternateKeyRequest Request(string key = "src1.contact", bool silent = false, bool dryRun = false)
    {
        return new ProcessSplitDataHubEntityAlternateKeyRequest
        {
            CorrelationId = "corr-split",
            EntityType = "Contact",
            EntityId = "entity-1",
            Key = key,
            Value = "source-1",
            Silent = silent,
            DryRun = dryRun
        };
    }

    private static SplitScenario CreateScenario()
    {
        var entity = Entity(("src1.contact", "source-1"), ("src2.contact", "source-1"));
        var init = DateTimeOffset.Parse("2026-06-07T09:00:00+10:00");
        var update = DateTimeOffset.Parse("2026-06-07T09:30:00+10:00");
        var initialData = new JObject
        {
            ["Name"] = "Initial",
            [nameof(DataHubEntity.alternateKeys)] = entity[nameof(DataHubEntity.alternateKeys)]!.DeepClone()
        };
        var currentData = new JObject
        {
            ["Name"] = "Current",
            [nameof(DataHubEntity.alternateKeys)] = entity[nameof(DataHubEntity.alternateKeys)]!.DeepClone()
        };

        return new SplitScenario(
            entity,
            [
                new ChangeTrackingEntry
                {
                    id = "tracking-init",
                    DataSource = DataSources.DataHub,
                    EntityType = "Contact",
                    EntityId = "entity-1",
                    EntryType = ChangeTrackingEntryTypes.Init,
                    Timestamp = init,
                    Data = initialData
                },
                new ChangeTrackingEntry
                {
                    id = "tracking-update",
                    DataSource = DataSources.DataHub,
                    EntityType = "Contact",
                    EntityId = "entity-1",
                    EntryType = ChangeTrackingEntryTypes.Update,
                    Timestamp = update,
                    Data = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(initialData, currentData)
                }
            ]);
    }

    private static JObject Entity(params (string Key, string Value)[] alternateKeys)
    {
        return new JObject
        {
            [nameof(DataHubEntity.id)] = "entity-1",
            [nameof(DataHubEntity.entityType)] = "Contact",
            [nameof(DataHubEntity.createdOn)] = DateTimeOffset.Parse("2026-06-07T09:00:00+10:00"),
            [nameof(DataHubEntity.lastUpdated)] = DateTimeOffset.Parse("2026-06-07T10:00:00+10:00"),
            ["Name"] = "Current",
            [nameof(DataHubEntity.alternateKeys)] = JArray.FromObject(alternateKeys.Select(key => new AlternateKey(key.Key, key.Value)).ToList())
        };
    }

    private static List<AlternateKey> AlternateKeys(JObject entity)
    {
        return entity[nameof(DataHubEntity.alternateKeys)]!.ToObject<List<AlternateKey>>()!;
    }

    private static ProcessSplitDataHubEntityAlternateKeyRequestHandler CreateHandler(RecordingMediator mediator)
    {
        var idService = Substitute.For<IIdService>();
        idService.NewId<DataHubEntity>().Returns("new-entity");
        idService.NewId<ChangeTrackingEntry>().Returns("new-tracking-1", "new-tracking-2", "new-tracking-3");

        var timeService = Substitute.For<ITimeService>();
        timeService.Now().Returns(DateTimeOffset.Parse("2026-06-07T11:00:00+10:00"));

        return new ProcessSplitDataHubEntityAlternateKeyRequestHandler(
            mediator,
            SuccessfulLockService(),
            idService,
            timeService);
    }

    private static IProcessingLockService SuccessfulLockService()
    {
        var service = Substitute.For<IProcessingLockService>();
        service.WaitForLockAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>())
            .Returns(Task.FromResult(new Response<ProcessingLock>(true, new ProcessingLock(), null)));
        service.ReleaseLockAsync(Arg.Any<ProcessingLock>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Response<Null>(true, new Null(), null)));
        return service;
    }

    private static T Capture<TValue, T>(TValue value, ref TValue? target, T response)
    {
        target = value;
        return response;
    }

    private sealed record SplitScenario(JObject Entity, List<ChangeTrackingEntry> TrackingEntries);

    private sealed class RecordingMediator(Func<IRequest, object> responseFactory) : IMediator
    {
        public List<IRequest> Requests { get; } = new();

        public Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<object> SendAndHandleExceptions<TRequest>(TRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public Task<TResponse> SendAndHandleExceptions<TResponse>(IRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            throw new NotSupportedException();
        }

        public Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            Requests.Add(request);
            return Task.FromResult(((TResponse?)responseFactory(request), (Exception?)null));
        }
    }
}
