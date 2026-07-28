using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Newtonsoft.Json.Linq;
using OneOf;
using Reimaginate.DataHub.Agent.Requests.Internal.SendMergeFailuresToDataHub;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.Requests.External.Client.RegisterAlerts;
using Reimaginate.DataHub.Requests.External.CLI.GetSyncFailuresWhere;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.GetLogs;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mapper;
using Reimaginate.Mediator;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class LoggingFailureReportingTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task LogEvents_should_report_partial_bulk_create_failures()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(LogEvents_should_report_partial_bulk_create_failures)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(LogEvents_should_report_partial_bulk_create_failures))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request =>
                    {
                        var command = (CreateCosmosDocumentsCommand<LogEntry>)request;
                        return new CreateCosmosDocumentsResponse<LogEntry>
                        {
                            Successes = [command.Documents[0]],
                            Failures = [new DataAccessFailure<LogEntry>(command.Documents[1], new Exception("cosmos write failed"))]
                        };
                    });
                    var handler = new LogEventsRequestHandler<Alert>(new StableIdService(), mediator);

                    var response = await handler.HandleAsync(new LogEventsRequest<Alert>
                    {
                        Events =
                        [
                            new Alert { Severity = "Warning", Subject = "one", Description = "first", Timestamp = DateTimeOffset.UtcNow },
                            new Alert { Severity = "Error", Subject = "two", Description = "second", Timestamp = DateTimeOffset.UtcNow }
                        ]
                    }, CancellationToken.None);

                    response.Success.Should().BeFalse();
                    response.FailureReason.Should().Contain("LOG_PERSISTENCE_FAILED");
                    response.Failures.Should().ContainSingle();
                    response.Failures[0].Error.Message.Should().Be("cosmos write failed");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task LogSyncEvents_should_report_partial_bulk_create_failures()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(LogSyncEvents_should_report_partial_bulk_create_failures)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(LogSyncEvents_should_report_partial_bulk_create_failures))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request =>
                    {
                        return request switch
                        {
                            GetLogsRequest<SyncFailure> => new GetLogsResponse { Results = [] },
                            GetLogsRequest<SyncSuccess> => new GetLogsResponse { Results = [] },
                            CreateCosmosDocumentsCommand<LogEntry> command => new CreateCosmosDocumentsResponse<LogEntry>
                            {
                                Successes = [],
                                Failures = [new DataAccessFailure<LogEntry>(command.Documents.Single(), new Exception("cosmos write failed"))]
                            },
                            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
                        };
                    });
                    var handler = new LogSyncEventsRequestHandler<SyncFailure>(new StableIdService(), mediator);

                    var response = await handler.HandleAsync(new LogSyncEventsRequest<SyncFailure>
                    {
                        SyncEvents =
                        [
                            new SyncFailure
                            {
                                DataSource = "CRM",
                                AgentId = "agent-1",
                                DataHubEntityType = "Contact",
                                DataHubEntityId = "dh-1",
                                FailureType = "SyncFailed",
                                FailureReason = "bad data",
                                Timestamp = DateTimeOffset.UtcNow
                            }
                        ]
                    }, CancellationToken.None);

                    response.Success.Should().BeFalse();
                    response.FailureReason.Should().Contain("LOG_PERSISTENCE_FAILED");
                    response.Failures.Should().ContainSingle();

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task GetSyncFailuresWhere_should_translate_typed_filter_once()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(GetSyncFailuresWhere_should_translate_typed_filter_once)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(GetSyncFailuresWhere_should_translate_typed_filter_once))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    GetCosmosDocumentsQuery<LogEntry>? capturedQuery = null;
                    var mediator = new RecordingMediator(request =>
                    {
                        capturedQuery = (GetCosmosDocumentsQuery<LogEntry>)request;
                        return new PagedResults<LogEntry> { Results = [] };
                    });
                    var mapper = Substitute.For<IMapper>();
                    mapper.MapAsync<List<SyncFailureDTO>>(
                            Arg.Any<List<LogEntry>>(),
                            Arg.Any<CancellationToken>(),
                            Arg.Any<Dictionary<string, object>?>())
                        .Returns(Task.FromResult(new List<SyncFailureDTO>()));
                    var handler = new GetSyncFailuresWhereRequestHandler(mediator, mapper);

                    await handler.HandleAsync(new GetSyncFailuresWhereRequest
                    {
                        WhereClause = "x.FailureReason = 'Expected failure'"
                    }, CancellationToken.None);

                    capturedQuery.Should().NotBeNull();
                    capturedQuery!.WhereClause.Should().Contain("x.Data.FailureReason = 'Expected failure'");
                    capturedQuery.WhereClause.Should().NotContain("x.FailureReason");
                    capturedQuery.WhereClause.Should().NotContain("x.Data.Data");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task SendMergeFailuresToDataHub_should_preserve_failure_reason_and_type()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(SendMergeFailuresToDataHub_should_preserve_failure_reason_and_type)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(SendMergeFailuresToDataHub_should_preserve_failure_reason_and_type))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    RegisterMergeFailuresRequest? capturedRequest = null;
                    var client = Substitute.For<IDataHubClient>();
                    client.PostRequestAsync<RegisterMergeFailuresRequest, NullResponse>(
                            Arg.Do<RegisterMergeFailuresRequest>(request => capturedRequest = request),
                            Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult(new NullResponse()));
                    var handler = new SendMergeFailuresToDataHubRequestHandler(
                        client,
                        Options.Create(new DataHubAgentConfig { AgentId = "agent-1", DataSource = "CRM" }));

                    await handler.HandleAsync(new SendMergeFailuresToDataHubRequest(
                    [
                        new MergeEntityResult
                        {
                            DataHubEntityType = "Contact",
                            DataHubEntityId = "dh-1",
                            SourceEntityType = "Lead",
                            SourceEntityId = "src-1",
                            MergeOutcome = "MergeRejected",
                            FailureReason = "pre-merge rule failed"
                        }
                    ]), CancellationToken.None);

                    capturedRequest.Should().NotBeNull();
                    var failure = capturedRequest!.MergeFailures.Should().ContainSingle().Subject;
                    failure.DataSource.Should().Be("CRM");
                    failure.AgentId.Should().Be("agent-1");
                    failure.FailureType.Should().Be("MergeRejected");
                    failure.FailureReason.Should().Be("pre-merge rule failed");
                    failure.Timestamp.Should().NotBe(default);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task RegisterAlerts_should_report_notification_failures_after_log_success()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(RegisterAlerts_should_report_notification_failures_after_log_success)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(RegisterAlerts_should_report_notification_failures_after_log_success))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var alert = new Alert
                    {
                        Severity = "Error",
                        Subject = "sync stalled",
                        Description = "stalled",
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    var mediator = Substitute.For<IMediator>();
                    mediator.TrySend<LogEventsResponse>(
                            Arg.Any<LogEventsRequest<Alert>>(),
                            Arg.Any<CancellationToken>(),
                            Arg.Any<Action<Exception>?>())
                        .Returns(Task.FromResult<(LogEventsResponse? Response, Exception? Exception)>((new LogEventsResponse
                        {
                            Success = true,
                            LogEntries = [new LogEntry { id = "log-1", Type = nameof(Alert), Data = JObject.FromObject(alert), Timestamp = alert.Timestamp }]
                        }, null)));
                    mediator.SendAsync(
                            Arg.Any<DispatchNotificationsRequest>(),
                            Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult<OneOf<DispatchNotificationsResponse, Exception>>(new DispatchNotificationsResponse
                        {
                            Success = false,
                            Failures = [new DispatchNotificationFailure(new AlertNotification(), new Exception("event grid rejected"))]
                        }));
                    var handler = new RegisterAlertsRequestHandler(mediator);

                    var act = () => handler.HandleAsync(new RegisterAlertsRequest { Alerts = [alert] }, CancellationToken.None);

                    var ex = await act.Should().ThrowAsync<DataHubException>();
                    ex.Which.Message.Should().Contain("NOTIFICATION_DISPATCH_FAILED");
                    ex.Which.Details.Should().ContainSingle(detail =>
                        detail.Code == "NOTIFICATION_DISPATCH_FAILED" &&
                        detail.Message == "event grid rejected");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private sealed class StableIdService : IIdService
    {
        private int _next;

        public string NewId<T>()
        {
            return $"log-{++_next}";
        }

        public string NewId(Type forType)
        {
            return $"log-{++_next}";
        }
    }

    private sealed class RecordingMediator(Func<IRequest, object> responseFactory) : IMediator
    {
        public Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<object> SendAndHandleExceptions<TRequest>(
            TRequest request,
            CancellationToken cancellationToken,
            Action<Exception>? exceptionHandler = null)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public Task<TResponse> SendAndHandleExceptions<TResponse>(
            IRequest request,
            CancellationToken cancellationToken,
            Action<Exception>? exceptionHandler = null)
        {
            throw new NotSupportedException();
        }

        public Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken,
            Action<Exception>? exceptionHandler = null)
        {
            return Task.FromResult(((TResponse?)responseFactory(request), (Exception?)null));
        }
    }
}
