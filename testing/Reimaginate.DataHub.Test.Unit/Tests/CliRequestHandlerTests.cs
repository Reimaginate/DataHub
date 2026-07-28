using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OneOf;
using Reimaginate.DataHub.Requests.External.CLI.RetryJob;
using Reimaginate.DataHub.Requests.External.CLI.SubmitJobs;
using Reimaginate.DataHub.Requests.External.CLI.UpdateDuplicate;
using Reimaginate.DataHub.Requests.External.CLI.UpdateDuplicates;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesById;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.Requests.Internal.ProcessGetJobs;
using Reimaginate.DataHub.Requests.Internal.ProcessRevertDataHubEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateDuplicates;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateJobs;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mapper;
using Reimaginate.Mediator;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using CliPatchEntityRequestHandler = Reimaginate.DataHub.Requests.External.CLI.PatchEntity.PatchEntityRequestHandler;
using CliRevertDataHubEntitiesRequestHandler = Reimaginate.DataHub.Requests.External.CLI.RevertDataHubEntities.RevertDataHubEntitiesRequestHandler;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class CliRequestHandlerTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task PatchEntity_should_forward_single_request_to_batch_processor()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(PatchEntity_should_forward_single_request_to_batch_processor)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(PatchEntity_should_forward_single_request_to_batch_processor))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var timestamp = DateTimeOffset.Parse("2026-06-07T10:30:00+10:00");
                    var patch = new Patch { Operation = "set", Path = "Status", Value = "Active" };
                    var mediator = new RecordingMediator(_ => new ProcessPatchEntitiesResponse
                    {
                        Results = [new ProcessPatchEntityResponse { Success = true, ChangeSet = JObject.Parse("""{"Status":["Draft","Active"]}""") }]
                    });
                    var handler = new CliPatchEntityRequestHandler(mediator);

                    var response = await handler.HandleAsync(new PatchEntityRequest
                    {
                        CorrelationId = "corr-1",
                        DataSource = DataSources.DataHub,
                        EntityType = "Venue",
                        EntityId = "venue-1",
                        Timestamp = timestamp,
                        Operations = [patch],
                        Silent = true,
                        DispatchNotifications = true
                    }, CancellationToken.None);

                    response.Success.Should().BeTrue();
                    response.Changed.Should().BeTrue();
                    response.DataSource.Should().Be(DataSources.DataHub);
                    response.EntityType.Should().Be("Venue");
                    response.EntityId.Should().Be("venue-1");

                    var processRequest = mediator.Requests.Should().ContainSingle().Subject.Should().BeOfType<ProcessPatchEntitiesRequest>().Subject;
                    processRequest.CorrelationId.Should().Be("corr-1");
                    processRequest.Silent.Should().BeTrue();
                    processRequest.DispatchNotifications.Should().BeTrue();

                    var innerRequest = processRequest.Requests.Should().ContainSingle().Subject;
                    innerRequest.DataSource.Should().Be(DataSources.DataHub);
                    innerRequest.EntityType.Should().Be("Venue");
                    innerRequest.EntityId.Should().Be("venue-1");
                    innerRequest.Timestamp.Should().Be(timestamp);
                    innerRequest.Operations.Should().ContainSingle().Which.Should().BeSameAs(patch);
                    innerRequest.Silent.Should().BeTrue();
                    innerRequest.DispatchNotifications.Should().BeTrue();
                    innerRequest.CommitToDb.Should().BeTrue();

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
    public async Task PatchEntity_should_return_batch_commit_failure()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(PatchEntity_should_return_batch_commit_failure)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(PatchEntity_should_return_batch_commit_failure))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(_ => new ProcessPatchEntitiesResponse
                    {
                        Results =
                        [
                            new ProcessPatchEntityResponse
                            {
                                Success = false,
                                FailureReason = "Failed to upsert DataHub entity: write failed"
                            }
                        ]
                    });
                    var handler = new CliPatchEntityRequestHandler(mediator);

                    var response = await handler.HandleAsync(new PatchEntityRequest
                    {
                        DataSource = DataSources.DataHub,
                        EntityType = "Venue",
                        EntityId = "venue-1",
                        Operations = [new Patch { Operation = "set", Path = "Status", Value = "Active" }]
                    }, CancellationToken.None);

                    response.Success.Should().BeFalse();
                    response.FailureReason.Should().Be("Failed to upsert DataHub entity: write failed");

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
    public async Task UpdateDuplicate_should_forward_duplicate_to_internal_update_request()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(UpdateDuplicate_should_forward_duplicate_to_internal_update_request)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(UpdateDuplicate_should_forward_duplicate_to_internal_update_request))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var duplicate = new Duplicate { id = "duplicate-1", Status = "Open" };
                    var mediator = new RecordingMediator(_ => new ProcessUpdateDuplicatesResponse { Success = true });
                    var handler = new UpdateDuplicateRequestHandler(mediator);

                    var response = await handler.HandleAsync(new UpdateDuplicateRequest { Duplicate = duplicate }, CancellationToken.None);

                    response.Success.Should().BeTrue();
                    var request = mediator.Requests.Should().ContainSingle().Subject.Should().BeOfType<ProcessUpdateDuplicatesRequest>().Subject;
                    request.Duplicates.Should().ContainSingle().Which.Should().BeSameAs(duplicate);

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
    public async Task UpdateDuplicates_should_propagate_internal_failure()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(UpdateDuplicates_should_propagate_internal_failure)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(UpdateDuplicates_should_propagate_internal_failure))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(_ => new ProcessUpdateDuplicatesResponse
                    {
                        Success = false,
                        FailureReason = "upsert failed"
                    });
                    var handler = new UpdateDuplicatesRequestHandler(mediator);

                    var response = await handler.HandleAsync(new UpdateDuplicatesRequest { Duplicates = [new Duplicate { id = "duplicate-1" }] }, CancellationToken.None);

                    response.Success.Should().BeFalse();
                    response.FailureReason.Should().Be("upsert failed");

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
    public async Task SubmitJobs_should_forward_jobs_user_and_notification_flag()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(SubmitJobs_should_forward_jobs_user_and_notification_flag)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(SubmitJobs_should_forward_jobs_user_and_notification_flag))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var job = new JobDTO { JobId = "job-1", Request = new JObject(), Status = JobsConstants.Statuses.Ready };
                    var user = new User { id = "user-1" };
                    var submitResult = new SubmitJobResult { Success = true, Result = job };
                    var mediator = new RecordingMediator(_ => new ProcessSubmitJobsResponse
                    {
                        Success = true,
                        Results = [submitResult]
                    });
                    var handler = new SubmitJobsRequestHandler(mediator);

                    var response = await handler.HandleAsync(new SubmitJobsRequest
                    {
                        Jobs = [job],
                        User = user,
                        DisableNotifications = true
                    }, CancellationToken.None);

                    response.Success.Should().BeTrue();
                    response.Results.Should().ContainSingle().Which.Should().BeSameAs(submitResult);
                    var request = mediator.Requests.Should().ContainSingle().Subject.Should().BeOfType<ProcessSubmitJobsRequest>().Subject;
                    request.Jobs.Should().ContainSingle().Which.Should().BeSameAs(job);
                    request.User.Should().BeSameAs(user);
                    request.DisableNotifications.Should().BeTrue();

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
    public async Task RetryJob_should_mark_existing_job_ready_and_clear_completion()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(RetryJob_should_mark_existing_job_ready_and_clear_completion)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(RetryJob_should_mark_existing_job_ready_and_clear_completion))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var now = DateTimeOffset.Parse("2026-06-07T10:30:00+10:00");
                    var job = new Job
                    {
                        id = "job-1",
                        Name = "Sync job",
                        Status = JobsConstants.Statuses.Complete,
                        CompletedOn = DateTimeOffset.Parse("2026-06-07T10:00:00+10:00"),
                        Request = new JObject(),
                        Response = new JObject()
                    };
                    var jobDto = new JobDTO
                    {
                        JobId = "job-1",
                        Name = job.Name,
                        Status = job.Status,
                        CompletedOn = job.CompletedOn,
                        Request = job.Request,
                        Response = job.Response
                    };
                    var mediator = new RecordingMediator(request => request switch
                    {
                        ProcessGetJobsRequest => new ProcessGetJobsResponse
                        {
                            Success = true,
                            PagedResults = new PagedResults<Job> { Results = [job], ResultCount = 1 }
                        },
                        ProcessUpdateJobsRequest => new ProcessUpdateJobsResponse { Success = true },
                        _ => throw new InvalidOperationException(request.GetType().FullName)
                    });
                    var mapper = Substitute.For<IMapper>();
                    mapper.MapAsync<JobDTO>(job, CancellationToken.None).Returns(Task.FromResult(jobDto));
                    var timeService = Substitute.For<ITimeService>();
                    timeService.Now().Returns(now);
                    var handler = new RetryJobRequestHandler(mediator, mapper, timeService);

                    var response = await handler.HandleAsync(new RetryJobRequest { JobId = "job-1" }, CancellationToken.None);

                    response.Success.Should().BeTrue();
                    var updateRequest = mediator.Requests.OfType<ProcessUpdateJobsRequest>().Should().ContainSingle().Subject;
                    updateRequest.Jobs.Should().ContainSingle().Which.Should().Match<JobDTO>(updated =>
                        updated.JobId == "job-1" &&
                        updated.Status == JobsConstants.Statuses.Ready &&
                        updated.CompletedOn == null &&
                        updated.LastUpdated == now);

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
    public async Task RetryJob_should_return_not_found_when_job_does_not_exist()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(RetryJob_should_return_not_found_when_job_does_not_exist)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(RetryJob_should_return_not_found_when_job_does_not_exist))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(_ => new ProcessGetJobsResponse
                    {
                        Success = true,
                        PagedResults = new PagedResults<Job> { Results = [], ResultCount = 0 }
                    });
                    var handler = new RetryJobRequestHandler(
                        mediator,
                        Substitute.For<IMapper>(),
                        Substitute.For<ITimeService>());

                    var response = await handler.HandleAsync(new RetryJobRequest { JobId = "missing-job" }, CancellationToken.None);

                    response.Success.Should().BeFalse();
                    response.FailureReason.Should().Be("NOT_FOUND");

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
    public async Task RevertDataHubEntities_should_forward_request_to_internal_processor()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(RevertDataHubEntities_should_forward_request_to_internal_processor)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(RevertDataHubEntities_should_forward_request_to_internal_processor))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var revertTo = DateTimeOffset.Parse("2026-06-07T10:30:00+10:00");
                    var mediator = new RecordingMediator(_ => new ProcessRevertDataHubEntitiesResponse
                    {
                        Results = [new RevertDataHubEntityResult { EntityType = "Contact", EntityId = "contact-1", Success = true }]
                    });
                    var handler = new CliRevertDataHubEntitiesRequestHandler(mediator);

                    var response = await handler.HandleAsync(new RevertDataHubEntitiesRequest
                    {
                        CorrelationId = "corr-revert",
                        EntityType = "Contact",
                        EntityIds = ["contact-1"],
                        RevertTo = revertTo,
                        TrackingEntryId = "tracking-1",
                        DispatchNotifications = false,
                        DryRun = true
                    }, CancellationToken.None);

                    response.Results.Should().ContainSingle(result => result.Success && result.EntityId == "contact-1");
                    var processRequest = mediator.Requests.Should().ContainSingle().Subject.Should().BeOfType<ProcessRevertDataHubEntitiesRequest>().Subject;
                    processRequest.CorrelationId.Should().Be("corr-revert");
                    processRequest.EntityType.Should().Be("Contact");
                    processRequest.EntityIds.Should().ContainSingle().Which.Should().Be("contact-1");
                    processRequest.RevertTo.Should().Be(revertTo);
                    processRequest.TrackingEntryId.Should().Be("tracking-1");
                    processRequest.DispatchNotifications.Should().BeFalse();
                    processRequest.DryRun.Should().BeTrue();

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
    public async Task ProcessRevertDataHubEntities_should_preview_reverse_change_for_date_target()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ProcessRevertDataHubEntities_should_preview_reverse_change_for_date_target)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ProcessRevertDataHubEntities_should_preview_reverse_change_for_date_target))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var init = DateTimeOffset.Parse("2026-06-07T09:00:00+10:00");
                    var target = init.AddMinutes(10);
                    var current = init.AddMinutes(20);
                    var trackingEntries = new List<ChangeTrackingEntry>
                    {
                        Entry(ChangeTrackingEntryTypes.Init, init, new JObject { ["Name"] = "Initial" }, id: "init"),
                        Entry(ChangeTrackingEntryTypes.Update, target, Diff(new JObject { ["Name"] = "Initial" }, new JObject { ["Name"] = "Target" }), id: "target"),
                        Entry(ChangeTrackingEntryTypes.Update, current, Diff(new JObject { ["Name"] = "Target" }, new JObject { ["Name"] = "Current" }), id: "current")
                    };
                    var mediator = new RecordingMediator(request => request switch
                    {
                        GetAllTrackingEntriesForEntitiesRequest => new GetAllTrackingEntriesForEntitiesResponse { TrackingEntries = trackingEntries },
                        _ => throw new InvalidOperationException(request.GetType().FullName)
                    });
                    var handler = new ProcessRevertDataHubEntitiesRequestHandler(mediator, Substitute.For<Reimaginate.ProcessingLockService.IProcessingLockService>(), Substitute.For<ITimeService>());

                    var response = await handler.HandleAsync(new ProcessRevertDataHubEntitiesRequest
                    {
                        EntityType = "Contact",
                        EntityIds = ["contact-1"],
                        RevertTo = target,
                        DryRun = true
                    }, CancellationToken.None);

                    var result = response.Results.Should().ContainSingle().Subject;
                    result.Success.Should().BeTrue();
                    result.Changed.Should().BeTrue();
                    result.ChangeSet!["Name"]!.Values<string>().Should().BeEquivalentTo(["Current", "Target"], options => options.WithStrictOrdering());

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
    public async Task ProcessRevertDataHubEntities_should_use_after_entry_semantics_for_tracking_entry_target()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ProcessRevertDataHubEntities_should_use_after_entry_semantics_for_tracking_entry_target)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ProcessRevertDataHubEntities_should_use_after_entry_semantics_for_tracking_entry_target))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var init = DateTimeOffset.Parse("2026-06-07T09:00:00+10:00");
                    var selectedTime = init.AddMinutes(10);
                    var trackingEntries = new List<ChangeTrackingEntry>
                    {
                        Entry(ChangeTrackingEntryTypes.Init, init, new JObject { ["Name"] = "Initial" }, id: "init"),
                        Entry(ChangeTrackingEntryTypes.Update, selectedTime, Diff(new JObject { ["Name"] = "Initial" }, new JObject { ["Name"] = "Selected" }), id: "selected"),
                        Entry(ChangeTrackingEntryTypes.Update, init.AddMinutes(20), Diff(new JObject { ["Name"] = "Selected" }, new JObject { ["Name"] = "Current" }), id: "current")
                    };
                    var mediator = new RecordingMediator(request => request switch
                    {
                        GetTrackingEntriesByIdQuery => new PagedResults<ChangeTrackingEntry> { Results = [trackingEntries[1]] },
                        GetAllTrackingEntriesForEntitiesRequest => new GetAllTrackingEntriesForEntitiesResponse { TrackingEntries = trackingEntries },
                        _ => throw new InvalidOperationException(request.GetType().FullName)
                    });
                    var handler = new ProcessRevertDataHubEntitiesRequestHandler(mediator, Substitute.For<Reimaginate.ProcessingLockService.IProcessingLockService>(), Substitute.For<ITimeService>());

                    var response = await handler.HandleAsync(new ProcessRevertDataHubEntitiesRequest
                    {
                        TrackingEntryId = "selected",
                        DryRun = true
                    }, CancellationToken.None);

                    var result = response.Results.Should().ContainSingle().Subject;
                    result.Success.Should().BeTrue();
                    result.RevertedTo.Should().Be(selectedTime);
                    result.ChangeSet!["Name"]!.Values<string>().Should().BeEquivalentTo(["Current", "Selected"], options => options.WithStrictOrdering());

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
    public async Task ProcessRevertDataHubEntities_should_return_failures_for_invalid_tracking_entry_targets()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ProcessRevertDataHubEntities_should_return_failures_for_invalid_tracking_entry_targets)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ProcessRevertDataHubEntities_should_return_failures_for_invalid_tracking_entry_targets))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var missingMediator = new RecordingMediator(_ => new PagedResults<ChangeTrackingEntry> { Results = [] });
                    var handler = new ProcessRevertDataHubEntitiesRequestHandler(missingMediator, Substitute.For<Reimaginate.ProcessingLockService.IProcessingLockService>(), Substitute.For<ITimeService>());

                    var missing = await handler.HandleAsync(new ProcessRevertDataHubEntitiesRequest { TrackingEntryId = "missing", DryRun = true }, CancellationToken.None);

                    missing.Results.Should().ContainSingle().Which.FailureReason.Should().Be("TRACKING_ENTRY_NOT_FOUND");

                    var sourceEntry = Entry(ChangeTrackingEntryTypes.Update, DateTimeOffset.Parse("2026-06-07T10:00:00+10:00"), new JObject(), dataSource: "CRM", id: "source-entry");
                    var sourceMediator = new RecordingMediator(_ => new PagedResults<ChangeTrackingEntry> { Results = [sourceEntry] });
                    handler = new ProcessRevertDataHubEntitiesRequestHandler(sourceMediator, Substitute.For<Reimaginate.ProcessingLockService.IProcessingLockService>(), Substitute.For<ITimeService>());

                    var source = await handler.HandleAsync(new ProcessRevertDataHubEntitiesRequest { TrackingEntryId = "source-entry", DryRun = true }, CancellationToken.None);

                    source.Results.Should().ContainSingle().Which.FailureReason.Should().Be("UNSUPPORTED_DATA_SOURCE");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static JObject Diff(JObject left, JObject right)
        => (JObject)Reimaginate.DataHub.Helpers.ChangeTrackingHelper.JsonDiffPatch.Diff(left, right);

    private static ChangeTrackingEntry Entry(string entryType, DateTimeOffset timestamp, JObject data, string dataSource = DataSources.DataHub, string id = "")
    {
        return new ChangeTrackingEntry
        {
            id = id,
            DataSource = dataSource,
            EntityType = "Contact",
            EntityId = "contact-1",
            EntryType = entryType,
            Timestamp = timestamp,
            Data = data
        };
    }

    private sealed class RecordingMediator : IMediator
    {
        private readonly Func<IRequest, object> _responseFactory;

        public RecordingMediator(Func<IRequest, object> responseFactory)
        {
            _responseFactory = responseFactory;
        }

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
            return Task.FromResult(((TResponse?)_responseFactory(request), (Exception?)null));
        }
    }
}
