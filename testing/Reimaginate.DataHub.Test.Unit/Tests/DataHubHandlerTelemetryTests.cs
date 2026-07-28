using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OneOf;
using Reimaginate.DataHub.AspNetCore;
using Reimaginate.DataHub.Config;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.Requests.Internal.DispatchJobs;
using Reimaginate.DataHub.Requests.Internal.GetLogs;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateJobs;
using Reimaginate.DataHub.Requests.Internal.RegisterJobs;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.Mapper;
using Reimaginate.Mediator;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DataHubHandlerTelemetryTests
{
    [Fact]
    public async Task ProcessSubmitJobs_should_record_job_status_metric()
    {
        using var capture = new MetricCapture("datahub.job.status.count");
        var job = new JobDTO
        {
            JobId = "job-1",
            Status = "Ready",
            Type = "DuplicateMerge",
            Target = "DataMaintenanceAgent",
            Request = new JObject()
        };
        var mediator = new RecordingMediator(request => request switch
        {
            RegisterJobsRequest => new RegisterJobsResponse
            {
                Results = [new RegisterJobResult { Success = true, Job = job }]
            },
            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
        });
        var handler = new ProcessSubmitJobsRequestHandler(mediator, new StableIdService());

        await handler.HandleAsync(new ProcessSubmitJobsRequest
        {
            Jobs = [job],
            DisableNotifications = true
        }, TestContext.Current.CancellationToken);

        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.job.status.count" &&
            Equals(metric.Tags["job.status"], "Ready") &&
            Equals(metric.Tags["job.type"], "DuplicateMerge") &&
            Equals(metric.Tags["job.target"], "DataMaintenanceAgent"));
    }

    [Fact]
    public async Task ProcessUpdateJobs_should_record_persistence_failure_metric()
    {
        using var capture = new MetricCapture("datahub.persistence.failure.count");
        var jobDto = new JobDTO
        {
            JobId = "job-1",
            Status = "Failed",
            Type = "Sync",
            Target = "CRM"
        };
        var job = new Job
        {
            id = "job-1",
            Status = jobDto.Status,
            Type = jobDto.Type,
            Target = jobDto.Target
        };
        var mapper = Substitute.For<IMapper>();
        mapper.MapAsync<List<Job>>(Arg.Any<List<JobDTO>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<Job> { job }));
        var mediator = new RecordingMediator(request => request switch
        {
            UpsertCosmosDocumentsCommand<Job> => new UpsertCosmosDocumentsResponse<Job>
            {
                Successes = [],
                Failures = [new DataAccessFailure<Job>(job, new Exception("cosmos write failed"))]
            },
            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
        });
        var handler = new ProcessUpdateJobsRequestHandler(mediator, mapper);

        await handler.HandleAsync(new ProcessUpdateJobsRequest { Jobs = [jobDto] }, TestContext.Current.CancellationToken);

        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.persistence.failure.count" &&
            metric.Value == 1 &&
            Equals(metric.Tags["operation"], "job_upsert") &&
            Equals(metric.Tags["document.type"], nameof(Job)));
    }

    [Fact]
    public async Task DispatchJobs_should_record_notification_failure_metric_when_event_grid_is_not_configured()
    {
        using var capture = new MetricCapture("datahub.notification.dispatch.failure.count", "datahub.domain_failure.count");
        var handler = new DispatchJobsRequestHandler(
            Options.Create(new NotificationServiceOptions { AzureEventGridClientOptions = new AzureEventGridClientOptions() }),
            new RecordingMediator(_ => throw new InvalidOperationException("Mediator should not be used.")),
            Substitute.For<IMapper>(),
            Substitute.For<ITimeService>());

        await handler.HandleAsync(new DispatchJobsRequest
        {
            Jobs =
            [
                new JobDTO { JobId = "job-1", Type = "Sync", Target = "CRM" },
                new JobDTO { JobId = "job-2", Type = "Sync", Target = "CRM" }
            ]
        }, TestContext.Current.CancellationToken);

        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.notification.dispatch.failure.count" &&
            metric.Value == 2 &&
            Equals(metric.Tags["notification.type"], "JobNotification"));
        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.domain_failure.count" &&
            metric.Value == 2 &&
            Equals(metric.Tags["failure.type"], "notification_dispatch"));
    }

    [Fact]
    public async Task LogEvents_should_record_domain_failure_metric_for_alerts()
    {
        using var capture = new MetricCapture("datahub.domain_failure.count");
        var mediator = new RecordingMediator(request =>
        {
            var command = (CreateCosmosDocumentsCommand<LogEntry>)request;
            return new CreateCosmosDocumentsResponse<LogEntry>
            {
                Successes = command.Documents,
                Failures = []
            };
        });
        var handler = new LogEventsRequestHandler<Alert>(new StableIdService(), mediator);

        await handler.HandleAsync(new LogEventsRequest<Alert>
        {
            Events = [new Alert { Severity = "Error", Subject = "sync stalled", Description = "stalled", Timestamp = DateTimeOffset.UtcNow }]
        }, TestContext.Current.CancellationToken);

        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.domain_failure.count" &&
            metric.Value == 1 &&
            Equals(metric.Tags["failure.type"], nameof(Alert)) &&
            Equals(metric.Tags["source"], "log_events"));
    }

    [Fact]
    public async Task LogSyncEvents_should_record_domain_failure_metric_for_sync_failures()
    {
        using var capture = new MetricCapture("datahub.domain_failure.count");
        var mediator = new RecordingMediator(request => request switch
        {
            GetLogsRequest<SyncFailure> => new GetLogsResponse { Results = [] },
            GetLogsRequest<SyncSuccess> => new GetLogsResponse { Results = [] },
            CreateCosmosDocumentsCommand<LogEntry> command => new CreateCosmosDocumentsResponse<LogEntry>
            {
                Successes = command.Documents,
                Failures = []
            },
            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
        });
        var handler = new LogSyncEventsRequestHandler<SyncFailure>(new StableIdService(), mediator);

        await handler.HandleAsync(new LogSyncEventsRequest<SyncFailure>
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
        }, TestContext.Current.CancellationToken);

        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.domain_failure.count" &&
            metric.Value == 1 &&
            Equals(metric.Tags["failure.type"], nameof(SyncFailure)) &&
            Equals(metric.Tags["source"], "log_sync_events"));
    }

    [Fact]
    public void Endpoint_diagnostics_should_record_processing_lock_timeout_metric()
    {
        using var capture = new MetricCapture("datahub.processing_lock.timeout.count");
        using var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            Response =
            {
                Body = new MemoryStream()
            }
        };

        _ = DataHubEndpointDiagnostics.CreateErrorResult(
            new DataHubException(
                DataHubErrorCategory.ServerError,
                "processing lock timed out",
                "PatchEntityRequest",
                "corr-lock"),
            context,
            NullLoggerFactory.Instance.CreateLogger("test"));

        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.processing_lock.timeout.count" &&
            Equals(metric.Tags["operation"], "PatchEntityRequest"));
    }

    private sealed class StableIdService : IIdService
    {
        private int _next;

        public string NewId<T>()
        {
            return $"id-{++_next}";
        }

        public string NewId(Type forType)
        {
            return $"id-{++_next}";
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
