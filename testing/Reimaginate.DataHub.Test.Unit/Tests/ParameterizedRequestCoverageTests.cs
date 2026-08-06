using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.FindMatchingEntities;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntityTypeCounts;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesWhere;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.Requests.External.CLI.GetAlertsById;
using Reimaginate.DataHub.Requests.External.CLI.DeserializeCliRequest;
using Reimaginate.DataHub.Requests.External.Client.DeserializeClientRequest;
using Reimaginate.DataHub.Requests.External.Client.GetUpdatedDataHubEntities;
using Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;
using Reimaginate.DataHub.Requests.Internal.GetDataSource;
using Reimaginate.DataHub.Requests.Internal.GetLogs;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.Requests.Internal.ProcessDeleteJobs;
using Reimaginate.DataHub.Requests.Internal.ProcessGetDuplicates;
using Reimaginate.DataHub.Requests.Internal.ProcessGetJobs;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mapper;
using Reimaginate.Mediator;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class ParameterizedRequestCoverageTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task All_client_requests_should_deserialize_query_parameters()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(All_client_requests_should_deserialize_query_parameters)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(All_client_requests_should_deserialize_query_parameters))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();
                    var failures = new List<string>();

                    foreach (var requestType in GetClientRequestTypes())
                    {
                        try
                        {
                            var request = await handler.HandleAsync(new DeserializeClientRequestRequest
                            {
                                SerializedRequest = new SerializedRequest
                                {
                                    RequestType = requestType.Name,
                                    CorrelationId = $"corr-{requestType.Name}",
                                    Data = JsonConvert.SerializeObject(new
                                    {
                                        Parameters = new object[]
                                        {
                                            new { Name = "id", Value = "entity-1" },
                                            new { Name = "enabled", Value = true },
                                            new { Name = "optional", Value = (object?)null }
                                        }
                                    })
                                }
                            }, CancellationToken.None);

                            request.GetType().Should().Be(requestType);
                            var clientRequest = request.Should().BeAssignableTo<IRequest>().Subject;
                            var parameters = (IReadOnlyCollection<DataHubQueryParameter>?)requestType.GetProperty(nameof(DataHubClientRequest<object>.Parameters))!.GetValue(clientRequest);
                            parameters.Should().HaveCount(3, $"{requestType.Name} should inherit parameter deserialization from DataHubClientRequest");
                            parameters.Should().Contain(parameter => parameter.Name == "id" && ParameterValueEquals(parameter.Value, "entity-1"));
                            parameters.Should().Contain(parameter => parameter.Name == "enabled" && ParameterValueEquals(parameter.Value, true));
                            parameters.Should().Contain(parameter => parameter.Name == "optional" && parameter.Value == null);
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{requestType.Name}: {ex.GetType().Name}: {ex.Message}");
                        }
                    }

                    failures.Should().BeEmpty();

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
    public async Task All_cli_requests_should_deserialize_query_parameters()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(All_cli_requests_should_deserialize_query_parameters)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(All_cli_requests_should_deserialize_query_parameters))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeCliRequestRequestHandler();
                    var failures = new List<string>();

                    foreach (var requestType in GetCliRequestTypes())
                    {
                        try
                        {
                            var request = await handler.HandleAsync(new DeserializeCliRequestRequest
                            {
                                SerializedRequest = new SerializedRequest
                                {
                                    RequestType = requestType.Name,
                                    CorrelationId = $"corr-{requestType.Name}",
                                    Data = JsonConvert.SerializeObject(new
                                    {
                                        Parameters = new object[]
                                        {
                                            new { Name = "id", Value = "entity-1" },
                                            new { Name = "enabled", Value = true },
                                            new { Name = "optional", Value = (object?)null }
                                        }
                                    })
                                }
                            }, CancellationToken.None);

                            request.GetType().Should().Be(requestType);
                            var cliRequest = request.Should().BeAssignableTo<DataHubCLIRequest>().Subject;
                            cliRequest.Parameters.Should().HaveCount(3, $"{requestType.Name} should inherit parameter deserialization from DataHubCLIRequest");
                            cliRequest.Parameters.Should().Contain(parameter => parameter.Name == "id" && ParameterValueEquals(parameter.Value, "entity-1"));
                            cliRequest.Parameters.Should().Contain(parameter => parameter.Name == "enabled" && ParameterValueEquals(parameter.Value, true));
                            cliRequest.Parameters.Should().Contain(parameter => parameter.Name == "optional" && parameter.Value == null);
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{requestType.Name}: {ex.GetType().Name}: {ex.Message}");
                        }
                    }

                    failures.Should().BeEmpty();

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
    public async Task DataHubQueryParameterMapper_should_unwrap_json_tokens_and_null_values()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DataHubQueryParameterMapper_should_unwrap_json_tokens_and_null_values)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DataHubQueryParameterMapper_should_unwrap_json_tokens_and_null_values))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var parameters = DataHubQueryParameterMapper.ToDataServiceParameters(
                    [
                        new DataHubQueryParameter { Name = "text", Value = new JValue("entity-1") },
                        new DataHubQueryParameter { Name = "shape", Value = JObject.FromObject(new { Id = "nested" }) },
                        new DataHubQueryParameter { Name = "optional", Value = null },
                        new DataHubQueryParameter { Name = "number", Value = 42 }
                    ]);

                    parameters.Should().Contain(parameter => parameter.Name == "text" && Equals(parameter.Value, "entity-1"));
                    parameters.Should().Contain(parameter => parameter.Name == "optional" && parameter.Value == null);
                    parameters.Should().Contain(parameter => parameter.Name == "number" && Equals(parameter.Value, 42));

                    var shape = parameters.Single(parameter => parameter.Name == "shape").Value.Should().BeOfType<JObject>().Subject;
                    shape.Value<string>("Id").Should().Be("nested");

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
    public async Task DataHubQueryParameterMapper_should_combine_non_null_parameter_sets()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DataHubQueryParameterMapper_should_combine_non_null_parameter_sets)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DataHubQueryParameterMapper_should_combine_non_null_parameter_sets))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var combined = DataHubQueryParameterMapper.Combine(
                        [new QueryParameter("internal", "value")],
                        null!,
                        [new QueryParameter("caller", 123)]);

                    combined.Should().Equal(
                        new QueryParameter("internal", "value"),
                        new QueryParameter("caller", 123));

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
    public async Task DataHubQueryParameterMapper_should_add_indexed_parameters()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DataHubQueryParameterMapper_should_add_indexed_parameters)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DataHubQueryParameterMapper_should_add_indexed_parameters))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var parameters = new List<QueryParameter>();

                    var parameterNames = DataHubQueryParameterMapper.AddIndexedParameters(["alpha'1", "beta"], "id", parameters);

                    parameterNames.Should().Equal("@id0", "@id1");
                    parameters.Should().Equal(
                        new QueryParameter("id0", "alpha'1"),
                        new QueryParameter("id1", "beta"));

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
    public async Task GetUpdatedDataHubEntities_should_parameterize_handler_owned_filter_values()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(GetUpdatedDataHubEntities_should_parameterize_handler_owned_filter_values)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(GetUpdatedDataHubEntities_should_parameterize_handler_owned_filter_values))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    GetDataHubEntitiesQuery? capturedQuery = null;
                    var mediator = new RecordingMediator(request =>
                    {
                        capturedQuery = request.Should().BeOfType<GetDataHubEntitiesQuery>().Subject;
                        return new PagedResults<JObject> { Results = [] };
                    });
                    var handler = new GetUpdatedDataHubEntitiesRequestHandler(mediator);
                    var fromDateTime = DateTimeOffset.Parse("2026-06-07T10:30:00+10:00");

                    await handler.HandleAsync(new GetUpdatedDataHubEntitiesRequest
                    {
                        EntityType = "Venue'One",
                        FromDateTime = fromDateTime,
                        Parameters = [new DataHubQueryParameter { Name = "caller", Value = "kept" }]
                    }, CancellationToken.None);

                    capturedQuery.Should().NotBeNull();
                    capturedQuery!.WhereClause.Should().Be("x.entityType = @entityType and x.lastUpdated >= @fromDateTime");
                    capturedQuery.Parameters.Should().Contain(parameter => parameter.Name == "entityType" && Equals(parameter.Value, "Venue'One"));
                    capturedQuery.Parameters.Should().Contain(parameter => parameter.Name == "fromDateTime" && Equals(parameter.Value, fromDateTime.ToString(DateFormats.ISO8601)));
                    capturedQuery.Parameters.Should().Contain(parameter => parameter.Name == "caller" && Equals(parameter.Value, "kept"));

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
    public async Task GetAlertsById_should_parameterize_type_and_id_list()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(GetAlertsById_should_parameterize_type_and_id_list)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(GetAlertsById_should_parameterize_type_and_id_list))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    GetCosmosDocumentsQuery<LogEntry>? capturedQuery = null;
                    var mediator = new RecordingMediator(request =>
                    {
                        capturedQuery = request.Should().BeOfType<GetCosmosDocumentsQuery<LogEntry>>().Subject;
                        return new PagedResults<LogEntry> { Results = [] };
                    });
                    var mapper = Substitute.For<IMapper>();
                    mapper.MapAsync<List<AlertDTO>>(Arg.Any<List<LogEntry>>(), Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult(new List<AlertDTO>()));
                    var handler = new GetAlertsByIdRequestHandler(mediator, mapper);

                    await handler.HandleAsync(new GetAlertsByIdRequest
                    {
                        Ids = ["alert'1", "alert-2"]
                    }, CancellationToken.None);

                    capturedQuery.Should().NotBeNull();
                    capturedQuery!.WhereClause.Should().Be("x.Type = @type and x.id in (@id0,@id1)");
                    capturedQuery.Parameters.Should().Contain(parameter => parameter.Name == "type" && Equals(parameter.Value, nameof(Alert)));
                    capturedQuery.Parameters.Should().Contain(parameter => parameter.Name == "id0" && Equals(parameter.Value, "alert'1"));
                    capturedQuery.Parameters.Should().Contain(parameter => parameter.Name == "id1" && Equals(parameter.Value, "alert-2"));

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
    public async Task Scalar_lookup_queries_should_parameterize_quote_containing_values()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Scalar_lookup_queries_should_parameterize_quote_containing_values)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Scalar_lookup_queries_should_parameterize_quote_containing_values))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request => request switch
                    {
                        GetCosmosDocumentsQuery<EntityConfig> => new PagedResults<EntityConfig> { Results = [] },
                        GetCosmosDocumentsQuery<DataSource> => new PagedResults<DataSource> { Results = [] },
                        _ => throw new NotSupportedException(request.GetType().FullName)
                    });

                    await new EntityConfigService(mediator).GetEntityConfig("Venue'Type", CancellationToken.None);
                    await new GetDataSourceRequestHandler(mediator).HandleAsync(new GetDataSourceRequest
                    {
                        DataSourceName = "CRM'Primary"
                    }, CancellationToken.None);

                    var entityConfigQuery = mediator.Requests.OfType<GetCosmosDocumentsQuery<EntityConfig>>().Single();
                    entityConfigQuery.WhereClause.Should().Be("x.EntityType = @entityType");
                    entityConfigQuery.Parameters.Should().Contain(parameter => parameter.Name == "entityType" && Equals(parameter.Value, "Venue'Type"));

                    var dataSourceQuery = mediator.Requests.OfType<GetCosmosDocumentsQuery<DataSource>>().Single();
                    dataSourceQuery.WhereClause.Should().Be("x.name = @dataSourceName");
                    dataSourceQuery.Parameters.Should().Contain(parameter => parameter.Name == "dataSourceName" && Equals(parameter.Value, "CRM'Primary"));

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
    public async Task Single_id_handlers_should_forward_parameterized_internal_requests()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Single_id_handlers_should_forward_parameterized_internal_requests)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Single_id_handlers_should_forward_parameterized_internal_requests))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request => request switch
                    {
                        ProcessGetJobsRequest => new ProcessGetJobsResponse
                        {
                            Success = true,
                            PagedResults = new PagedResults<Job> { Results = [] }
                        },
                        ProcessGetDuplicatesRequest => new ProcessGetDuplicatesResponse
                        {
                            Success = true,
                            PagedResults = new PagedResults<Duplicate> { Results = [] }
                        },
                        Reimaginate.DataHub.Requests.Internal.ProcessDeleteDuplicates.ProcessDeleteDuplicatesRequest => new Reimaginate.DataHub.Requests.Internal.ProcessDeleteDuplicates.ProcessDeleteDuplicatesResponse
                        {
                            Success = true
                        },
                        _ => throw new NotSupportedException(request.GetType().FullName)
                    });
                    var mapper = Substitute.For<IMapper>();

                    await new Reimaginate.DataHub.Requests.External.Client.GetJob.GetJobRequestHandler(mediator, mapper)
                        .HandleAsync(new Reimaginate.DataHub.SharedModels.Requests.Client.GetJobRequest { JobId = "job'1" }, CancellationToken.None);
                    await new Reimaginate.DataHub.Requests.External.Client.GetDuplicate.GetDuplicateRequestHandler(mediator)
                        .HandleAsync(new Reimaginate.DataHub.SharedModels.Requests.Client.GetDuplicateRequest { Id = "dup'1" }, CancellationToken.None);
                    await new Reimaginate.DataHub.Requests.External.Client.DeleteDuplicate.DeleteDuplicateRequestHandler(mediator)
                        .HandleAsync(new Reimaginate.DataHub.SharedModels.Requests.Client.DeleteDuplicateRequest { Id = "dup'2" }, CancellationToken.None);

                    var jobRequest = mediator.Requests.OfType<ProcessGetJobsRequest>().Single();
                    jobRequest.Where.Should().Be("x.id = @jobId");
                    jobRequest.Parameters.Should().Contain(parameter => parameter.Name == "jobId" && Equals(parameter.Value, "job'1"));

                    var duplicateRequest = mediator.Requests.OfType<ProcessGetDuplicatesRequest>().Single();
                    duplicateRequest.Where.Should().Be("x.id = @id");
                    duplicateRequest.Parameters.Should().Contain(parameter => parameter.Name == "id" && Equals(parameter.Value, "dup'1"));

                    var deleteDuplicateRequest = mediator.Requests.OfType<Reimaginate.DataHub.Requests.Internal.ProcessDeleteDuplicates.ProcessDeleteDuplicatesRequest>().Single();
                    deleteDuplicateRequest.Where.Should().Be("x.id = @id");
                    deleteDuplicateRequest.Parameters.Should().Contain(parameter => parameter.Name == "id" && Equals(parameter.Value, "dup'2"));

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
    public async Task In_list_handlers_should_generate_one_parameter_per_id()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(In_list_handlers_should_generate_one_parameter_per_id)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(In_list_handlers_should_generate_one_parameter_per_id))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request => request switch
                    {
                        GetCosmosDocumentsQuery<Job> => new PagedResults<Job> { Results = [] },
                        GetCosmosDocumentsQuery<LogEntry> => new PagedResults<LogEntry> { Results = [] },
                        DeleteCosmosDocumentsCommand<LogEntry> => new DeleteCosmosDocumentsResponse<LogEntry> { Successes = [], Failures = [] },
                        _ => throw new NotSupportedException(request.GetType().FullName)
                    });

                    await new ProcessDeleteJobsRequestHandler(mediator).HandleAsync(new ProcessDeleteJobsRequest
                    {
                        JobIds = ["job'1", "job-2"]
                    }, CancellationToken.None);
                    await new DeleteLogEntriesRequestHandler(mediator).HandleAsync(new Reimaginate.DataHub.Requests.Internal.DeleteLogEntries.DeleteLogEntriesRequest
                    {
                        Ids = ["log'1", "log'1", "log-2"]
                    }, CancellationToken.None);
                    await new Reimaginate.DataHub.Requests.External.Client.GetLogEntriesById.GetLogEntriesByIdRequestHandler(mediator)
                        .HandleAsync(new GetLogEntriesByIdRequest { Ids = ["entry'1", "entry-2"] }, CancellationToken.None);

                    var jobQuery = mediator.Requests.OfType<GetCosmosDocumentsQuery<Job>>().Single();
                    jobQuery.WhereClause.Should().Be("x.id in (@jobId0,@jobId1)");
                    jobQuery.Parameters.Should().Contain(parameter => parameter.Name == "jobId0" && Equals(parameter.Value, "job'1"));
                    jobQuery.Parameters.Should().Contain(parameter => parameter.Name == "jobId1" && Equals(parameter.Value, "job-2"));

                    var logQueries = mediator.Requests.OfType<GetCosmosDocumentsQuery<LogEntry>>().ToList();
                    logQueries[0].WhereClause.Should().Be("x.id in (@id0,@id1)");
                    logQueries[0].Parameters.Should().Contain(parameter => parameter.Name == "id0" && Equals(parameter.Value, "log'1"));
                    logQueries[0].Parameters.Should().Contain(parameter => parameter.Name == "id1" && Equals(parameter.Value, "log-2"));

                    logQueries[1].WhereClause.Should().Be("x.id in (@id0,@id1)");
                    logQueries[1].Parameters.Should().Contain(parameter => parameter.Name == "id0" && Equals(parameter.Value, "entry'1"));
                    logQueries[1].Parameters.Should().Contain(parameter => parameter.Name == "id1" && Equals(parameter.Value, "entry-2"));

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
    public async Task Failure_by_id_handlers_should_parameterize_type_and_id_lists()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Failure_by_id_handlers_should_parameterize_type_and_id_lists)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Failure_by_id_handlers_should_parameterize_type_and_id_lists))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request => request switch
                    {
                        GetCosmosDocumentsQuery<LogEntry> => new PagedResults<LogEntry> { Results = [] },
                        _ => throw new NotSupportedException(request.GetType().FullName)
                    });
                    var mapper = Substitute.For<IMapper>();
                    mapper.MapAsync<List<PatchFailureDTO>>(Arg.Any<List<LogEntry>>(), Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult(new List<PatchFailureDTO>()));
                    mapper.MapAsync<List<MergeFailureDTO>>(Arg.Any<List<LogEntry>>(), Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult(new List<MergeFailureDTO>()));
                    mapper.MapAsync<List<SyncFailureDTO>>(Arg.Any<List<LogEntry>>(), Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult(new List<SyncFailureDTO>()));

                    await new Reimaginate.DataHub.Requests.External.CLI.GetPatchFailuresById.GetPatchFailuresByIdRequestHandler(mediator, mapper)
                        .HandleAsync(new GetPatchFailuresByIdRequest { Ids = ["patch'1", "patch-2"] }, CancellationToken.None);
                    await new Reimaginate.DataHub.Requests.External.CLI.GetMergeFailuresById.GetMergeFailuresByIdRequestHandler(mediator, mapper)
                        .HandleAsync(new GetMergeFailuresByIdRequest { Ids = ["merge'1", "merge-2"] }, CancellationToken.None);
                    await new Reimaginate.DataHub.Requests.External.CLI.GetSyncFailuresById.GetSyncFailuresByIdRequestHandler(mediator, mapper)
                        .HandleAsync(new GetSyncFailuresByIdRequest { Ids = ["sync'1", "sync-2"] }, CancellationToken.None);

                    var queries = mediator.Requests.OfType<GetCosmosDocumentsQuery<LogEntry>>().ToList();
                    AssertTypedIdQuery(queries[0], nameof(PatchFailure), "patch'1", "patch-2");
                    AssertTypedIdQuery(queries[1], nameof(MergeFailure), "merge'1", "merge-2");
                    AssertTypedIdQuery(queries[2], nameof(SyncFailure), "sync'1", "sync-2");

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
    public async Task Raw_where_composition_should_keep_caller_filters_and_parameters()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Raw_where_composition_should_keep_caller_filters_and_parameters)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Raw_where_composition_should_keep_caller_filters_and_parameters))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request => request switch
                    {
                        GetDataHubEntitiesWhereRequest => new GetDataHubEntitiesResponse { Results = [] },
                        GetCosmosDocumentsQuery<LogEntry> => new PagedResults<LogEntry> { Results = [] },
                        _ => throw new NotSupportedException(request.GetType().FullName)
                    });
                    var mapper = Substitute.For<IMapper>();
                    mapper.MapAsync<List<AlertDTO>>(Arg.Any<List<LogEntry>>(), Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult(new List<AlertDTO>()));

                    await new Reimaginate.DataHub.Requests.External.Client.PatchDataHubEntitiesWhere.PatchDataHubEntitiesWhereRequestHandler(mediator)
                        .HandleAsync(new Reimaginate.DataHub.SharedModels.Requests.Client.PatchDataHubEntitiesWhereRequest
                        {
                            EntityType = "Venue'Type",
                            Where = "x.Owner = @owner",
                            Parameters = [new DataHubQueryParameter { Name = "owner", Value = "Ann'O" }]
                        }, CancellationToken.None);
                    await new Reimaginate.DataHub.Requests.External.CLI.GetAlertsWhere.GetAlertsWhereRequestHandler(mediator, mapper)
                        .HandleAsync(new GetAlertsWhereRequest
                        {
                            WhereClause = "x.Owner = @owner",
                            Parameters = [new DataHubQueryParameter { Name = "owner", Value = "Ann'O" }]
                        }, CancellationToken.None);

                    var patchQuery = mediator.Requests.OfType<GetDataHubEntitiesWhereRequest>().Single();
                    patchQuery.WhereClause.Should().Be("x.entityType = @entityType and (x.Owner = @owner)");
                    patchQuery.Parameters.Should().Contain(parameter => parameter.Name == "entityType" && Equals(parameter.Value, "Venue'Type"));
                    patchQuery.Parameters.Should().Contain(parameter => parameter.Name == "owner" && Equals(parameter.Value, "Ann'O"));

                    var alertQuery = mediator.Requests.OfType<GetCosmosDocumentsQuery<LogEntry>>().Single();
                    alertQuery.WhereClause.Should().Be("x.Type = @type and x.Owner = @owner");
                    alertQuery.Parameters.Should().Contain(parameter => parameter.Name == "type" && Equals(parameter.Value, nameof(Alert)));
                    alertQuery.Parameters.Should().Contain(parameter => parameter.Name == "owner" && Equals(parameter.Value, "Ann'O"));

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
    public async Task Log_queries_should_parameterize_type_data_source_and_entity_ids()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Log_queries_should_parameterize_type_data_source_and_entity_ids)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Log_queries_should_parameterize_type_data_source_and_entity_ids))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request => request switch
                    {
                        GetLogsRequest<SyncFailure> => new GetLogsResponse { Results = [] },
                        GetLogsRequest<SyncSuccess> => new GetLogsResponse { Results = [] },
                        GetCosmosDocumentsQuery<LogEntry> => new PagedResults<LogEntry> { Results = [] },
                        CreateCosmosDocumentsCommand<LogEntry> => new CreateCosmosDocumentsResponse<LogEntry> { Successes = [], Failures = [] },
                        _ => throw new NotSupportedException(request.GetType().FullName)
                    });
                    var idService = Substitute.For<IIdService>();
                    idService.NewId<LogEntry>().Returns("log-entry-1", "log-entry-2");

                    await new LogSyncEventsRequestHandler<SyncFailure>(idService, mediator).HandleAsync(new LogSyncEventsRequest<SyncFailure>
                    {
                        SyncEvents =
                        [
                            new SyncFailure { DataSource = "CRM'Primary", DataHubEntityType = "Venue", DataHubEntityId = "entity'1" },
                            new SyncFailure { DataSource = "CRM'Primary", DataHubEntityType = "Venue", DataHubEntityId = "entity-2" }
                        ]
                    }, CancellationToken.None);
                    await new GetLogsRequestHandler<SyncFailure>(mediator).HandleAsync(new GetLogsRequest<SyncFailure>
                    {
                        WhereClause = "x.Data.Owner = @owner",
                        Parameters = [new QueryParameter("owner", "Ann'O")]
                    }, CancellationToken.None);

                    var syncFailureCleanupQuery = mediator.Requests.OfType<GetLogsRequest<SyncFailure>>().First();
                    syncFailureCleanupQuery.WhereClause.Should().Be("x.Data.DataSource = @dataSource and x.Data.DataHubEntityId in (@entityId0,@entityId1)");
                    syncFailureCleanupQuery.Parameters.Should().Contain(parameter => parameter.Name == "dataSource" && Equals(parameter.Value, "CRM'Primary"));
                    syncFailureCleanupQuery.Parameters.Should().Contain(parameter => parameter.Name == "entityId0" && Equals(parameter.Value, "entity'1"));
                    syncFailureCleanupQuery.Parameters.Should().Contain(parameter => parameter.Name == "entityId1" && Equals(parameter.Value, "entity-2"));

                    var getLogsQuery = mediator.Requests.OfType<GetCosmosDocumentsQuery<LogEntry>>().Single();
                    getLogsQuery.WhereClause.Should().Be("x.Type = @type and x.Data.Owner = @owner");
                    getLogsQuery.Parameters.Should().Contain(parameter => parameter.Name == "type" && Equals(parameter.Value, nameof(SyncFailure)));
                    getLogsQuery.Parameters.Should().Contain(parameter => parameter.Name == "owner" && Equals(parameter.Value, "Ann'O"));

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
    public async Task GetDataHubEntitiesQueryHandler_should_use_parameterized_path_only_when_parameters_exist()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(GetDataHubEntitiesQueryHandler_should_use_parameterized_path_only_when_parameters_exist)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(GetDataHubEntitiesQueryHandler_should_use_parameterized_path_only_when_parameters_exist))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var dataService = Substitute.For<IDataHubEntityDataService>();
                    dataService.PagedWhereAsync(
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<int>(),
                            Arg.Any<string>(),
                            Arg.Any<bool>(),
                            Arg.Any<CancellationToken>())
                        .Returns(new PagedResults<JObject>());
                    dataService.PagedWhereParameterizedAsync(
                            Arg.Any<string>(),
                            Arg.Any<IEnumerable<QueryParameter>>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<int>(),
                            Arg.Any<string>(),
                            Arg.Any<bool>(),
                            Arg.Any<CancellationToken>())
                        .Returns(new PagedResults<JObject>());
                    var handler = new GetDataHubEntitiesQueryHandler(ServiceProvider(dataService));

                    await handler.HandleAsync(new GetDataHubEntitiesQuery { WhereClause = "x.id = 'legacy'" }, CancellationToken.None);
                    await dataService.Received(1).PagedWhereAsync(
                        "x.id = 'legacy'",
                        Arg.Any<string>(),
                        "x",
                        Arg.Any<string>(),
                        100,
                        Arg.Any<string>(),
                        false,
                        Arg.Any<CancellationToken>());

                    await handler.HandleAsync(new GetDataHubEntitiesQuery
                    {
                        WhereClause = "x.id = @id",
                        Parameters = [new QueryParameter("id", "entity-1")]
                    }, CancellationToken.None);
                    await dataService.Received(1).PagedWhereParameterizedAsync(
                        "x.id = @id",
                        Arg.Is<IEnumerable<QueryParameter>>(parameters => ContainsParameter(parameters, "id", "entity-1")),
                        Arg.Any<string>(),
                        "x",
                        Arg.Any<string>(),
                        100,
                        Arg.Any<string>(),
                        false,
                        Arg.Any<CancellationToken>());

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
    public async Task GetTrackingEntriesWhereQueryHandler_should_use_parameterized_path_only_when_parameters_exist()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(GetTrackingEntriesWhereQueryHandler_should_use_parameterized_path_only_when_parameters_exist)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(GetTrackingEntriesWhereQueryHandler_should_use_parameterized_path_only_when_parameters_exist))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var dataService = Substitute.For<IPartitionedDataService<ChangeTrackingEntry>>();
                    dataService.PagedWhereAsync(
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<int>(),
                            Arg.Any<string>(),
                            Arg.Any<bool>(),
                            Arg.Any<CancellationToken>())
                        .Returns(new PagedResults<ChangeTrackingEntry>());
                    dataService.PagedWhereParameterizedAsync(
                            Arg.Any<string>(),
                            Arg.Any<IEnumerable<QueryParameter>>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<int>(),
                            Arg.Any<string>(),
                            Arg.Any<bool>(),
                            Arg.Any<CancellationToken>())
                        .Returns(new PagedResults<ChangeTrackingEntry>());
                    var handler = new GetTrackingEntriesWhereQueryHandler(dataService);

                    await handler.HandleAsync(new GetTrackingEntriesWhereQuery { WhereClause = "x.EntityType = 'DHType'" }, CancellationToken.None);
                    await handler.HandleAsync(new GetTrackingEntriesWhereQuery
                    {
                        WhereClause = "x.EntityType = @entityType",
                        Parameters = [new QueryParameter("entityType", "DHType")]
                    }, CancellationToken.None);

                    await dataService.Received(1).PagedWhereAsync(
                        "x.EntityType = 'DHType'",
                        Arg.Any<string>(),
                        "x",
                        Arg.Any<string>(),
                        100,
                        Arg.Any<string>(),
                        false,
                        Arg.Any<CancellationToken>());
                    await dataService.Received(1).PagedWhereParameterizedAsync(
                        "x.EntityType = @entityType",
                        Arg.Is<IEnumerable<QueryParameter>>(parameters => ContainsParameter(parameters, "entityType", "DHType")),
                        Arg.Any<string>(),
                        "x",
                        Arg.Any<string>(),
                        100,
                        Arg.Any<string>(),
                        false,
                        Arg.Any<CancellationToken>());

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
    public async Task GetDataHubEntityTypeCountsQueryHandler_should_use_parameterized_execute_query_only_when_parameters_exist()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(GetDataHubEntityTypeCountsQueryHandler_should_use_parameterized_execute_query_only_when_parameters_exist)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(GetDataHubEntityTypeCountsQueryHandler_should_use_parameterized_execute_query_only_when_parameters_exist))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var dataService = Substitute.For<IDataHubEntityDataService>();
                    dataService.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                        .Returns(new List<object> { JObject.FromObject(new { entityType = "DHType", count = 1 }) });
                    dataService.ExecuteParameterizedQueryAsync(
                            Arg.Any<string>(),
                            Arg.Any<IEnumerable<QueryParameter>>(),
                            Arg.Any<CancellationToken>())
                        .Returns(new List<object> { JObject.FromObject(new { entityType = "DHType", count = 1 }) });
                    var handler = new GetDataHubEntityTypeCountsQueryHandler(ServiceProvider(dataService));

                    await handler.HandleAsync(new GetDataHubEntityTypeCountsQuery { WhereClause = "true" }, CancellationToken.None);
                    await handler.HandleAsync(new GetDataHubEntityTypeCountsQuery
                    {
                        WhereClause = "x.entityType = @entityType",
                        Parameters = [new QueryParameter("entityType", "DHType")]
                    }, CancellationToken.None);

                    await dataService.Received(1).ExecuteQueryAsync(
                        "SELECT x.entityType, COUNT(x) AS count FROM x where true GROUP BY x.entityType",
                        Arg.Any<CancellationToken>());
                    await dataService.Received(1).ExecuteParameterizedQueryAsync(
                        "SELECT x.entityType, COUNT(x) AS count FROM x where x.entityType = @entityType GROUP BY x.entityType",
                        Arg.Is<IEnumerable<QueryParameter>>(parameters => ContainsParameter(parameters, "entityType", "DHType")),
                        Arg.Any<CancellationToken>());

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
    public async Task GetCosmosDocumentsQueryHandler_should_add_document_type_parameter_to_caller_parameters()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(GetCosmosDocumentsQueryHandler_should_add_document_type_parameter_to_caller_parameters)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(GetCosmosDocumentsQueryHandler_should_add_document_type_parameter_to_caller_parameters))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var dataService = Substitute.For<IPartitionedDataService<Job>>();
                    dataService.PagedWhereParameterizedAsync<Job>(
                            Arg.Any<string>(),
                            Arg.Any<IEnumerable<QueryParameter>>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<int>(),
                            Arg.Any<string>(),
                            Arg.Any<bool>(),
                            Arg.Any<CancellationToken>())
                        .Returns(new PagedResults<Job>());
                    var handler = new GetCosmosDocumentsQueryHandler<Job>(ServiceProvider(dataService));

                    await handler.HandleAsync(new GetCosmosDocumentsQuery<Job>
                    {
                        WhereClause = "x.Status = @status",
                        Parameters = [new QueryParameter("status", "Ready")]
                    }, CancellationToken.None);

                    await dataService.Received(1).PagedWhereParameterizedAsync<Job>(
                        "x._dt = @__dataHubDocumentType and x.Status = @status",
                        Arg.Is<IEnumerable<QueryParameter>>(parameters =>
                            ContainsParameter(parameters, "__dataHubDocumentType", nameof(Job)) &&
                            ContainsParameter(parameters, "status", "Ready")),
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        100,
                        Arg.Any<string>(),
                        false,
                        Arg.Any<CancellationToken>());

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
    public async Task FindMatchingEntitiesQueryHandler_should_add_document_and_entity_type_parameters_to_caller_parameters()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(FindMatchingEntitiesQueryHandler_should_add_document_and_entity_type_parameters_to_caller_parameters)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(FindMatchingEntitiesQueryHandler_should_add_document_and_entity_type_parameters_to_caller_parameters))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var dataService = Substitute.For<IDataHubEntityDataService>();
                    dataService.WhereParameterizedAsync(
                            Arg.Any<string>(),
                            Arg.Any<IEnumerable<QueryParameter>>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<CancellationToken>())
                        .Returns(new WhereResults<JObject> { Results = [new JObject { ["id"] = "entity-1" }] });
                    var handler = new FindMatchingEntitiesQueryHandler(ServiceProvider(dataService));

                    var response = await handler.HandleAsync(new FindMatchingEntitiesQuery
                    {
                        EntityType = "DHType",
                        WhereClause = "x.ValueA = @value",
                        Parameters = [new QueryParameter("value", "match")]
                    }, CancellationToken.None);

                    response.Should().ContainSingle();
                    await dataService.Received(1).WhereParameterizedAsync(
                        "x._dt = @__dataHubDocumentType and x.entityType = @__dataHubEntityType and (x.ValueA = @value)",
                        Arg.Is<IEnumerable<QueryParameter>>(parameters =>
                            ContainsParameter(parameters, "__dataHubDocumentType", nameof(DataHubEntity)) &&
                            ContainsParameter(parameters, "__dataHubEntityType", "DHType") &&
                            ContainsParameter(parameters, "value", "match")),
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<CancellationToken>());

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
    public async Task Parameterized_mapper_call_site_guard_should_match_the_explicit_test_matrix()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Parameterized_mapper_call_site_guard_should_match_the_explicit_test_matrix)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Parameterized_mapper_call_site_guard_should_match_the_explicit_test_matrix))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var expected = new Dictionary<string, int>
                    {
                        ["DataAccess/Queries/FindMatchingEntities/FindMatchingEntitiesQueryHandler.cs|Combine"] = 1,
                        ["DataAccess/Queries/GetCosmosDocuments/GetCosmosDocumentsQueryHandler.cs|Combine"] = 1,
                        ["Requests/Internal/GetLogs/GetLogsRequestHandler.cs|Combine"] = 1,
                        ["Requests/External/Client/GetJobs/GetJobsRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/Client/DeleteJobs/DeleteJobsRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/Client/GetDataHubEntitiesWhere/GetEntitiesWhereRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/Client/DeleteDuplicates/DeleteDuplicatesRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/Client/GetDuplicates/GetDuplicatesRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/Client/GetDataHubEntityTypeCounts/GetDataHubEntityTypeCountsHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/Client/GetTrackingData/GetTrackingDataRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/Client/GetLogEntriesWhere/GetLogEntriesWhereRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/Client/GetUpdatedDataHubEntities/GetUpdatedDataHubEntitiesRequestHandler.cs|Combine"] = 1,
                        ["Requests/External/Client/GetUpdatedDataHubEntities/GetUpdatedDataHubEntitiesRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/DeleteDuplicates/DeleteDuplicatesRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/DeleteJobs/DeleteJobsRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetDataHubEntityTypeCounts/GetDataHubEntityTypeCountsRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetAlertsWhere/GetAlertsWhereRequestHandler.cs|Combine"] = 1,
                        ["Requests/External/CLI/GetAlertsWhere/GetAlertsWhereRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetJobs/GetJobsRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetPatchFailuresWhere/GetPatchFailuresWhereRequestHandler.cs|Combine"] = 1,
                        ["Requests/External/CLI/GetPatchFailuresWhere/GetPatchFailuresWhereRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetMergeFailuresWhere/GetMergeFailuresWhereRequestHandler.cs|Combine"] = 1,
                        ["Requests/External/CLI/GetMergeFailuresWhere/GetMergeFailuresWhereRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetDuplicates/GetDuplicatesRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetLogEntriesWhere/GetLogEntriesWhereRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetEntitiesWhere/GetEntitiesWhereRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetMergeMarkers/GetMergeMarkersRequestHandler.cs|ToDataServiceParameters"] = 2,
                        ["Requests/External/CLI/GetRoles/GetRolesRequestHandler.cs|Combine"] = 1,
                        ["Requests/External/CLI/GetRoles/GetRolesRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetUsers/GetUsersRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/DeleteResolutionPromises/DeleteResolutionPromisesRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/ListResolutionPromises/ListResolutionPromisesRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/ResolveResolutionPromises/ResolveResolutionPromisesRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetSyncFailuresWhere/GetSyncFailuresWhereRequestHandler.cs|Combine"] = 1,
                        ["Requests/External/CLI/GetSyncFailuresWhere/GetSyncFailuresWhereRequestHandler.cs|ToDataServiceParameters"] = 1,
                        ["Requests/External/CLI/GetSyncMarkers/GetSyncMarkersRequestHandler.cs|ToDataServiceParameters"] = 2
                    };

                    var actual = FindMapperCallSites()
                        .GroupBy(callSite => callSite)
                        .ToDictionary(group => group.Key, group => group.Count());

                    actual.Should().BeEquivalentTo(expected, "new parameterized request mapping call sites must be added to this coverage matrix");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static IReadOnlyList<Type> GetClientRequestTypes()
    {
        return typeof(DataHubClientRequest<>).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Where(IsDataHubClientRequestType)
            .OrderBy(type => type.FullName)
            .ToList();
    }

    private static IReadOnlyList<Type> GetCliRequestTypes()
    {
        return typeof(DataHubCLIRequest).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Where(type => typeof(DataHubCLIRequest).IsAssignableFrom(type))
            .OrderBy(type => type.FullName)
            .ToList();
    }

    private static bool IsDataHubClientRequestType(Type type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(DataHubClientRequest<>))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsParameter(IEnumerable<QueryParameter>? parameters, string name, object? value)
    {
        return parameters?.Any(parameter => parameter.Name == name && Equals(parameter.Value, value)) == true;
    }

    private static void AssertTypedIdQuery(GetCosmosDocumentsQuery<LogEntry> query, string type, string firstId, string secondId)
    {
        query.WhereClause.Should().Be("x.Type = @type and x.id in (@id0,@id1)");
        query.Parameters.Should().Contain(parameter => parameter.Name == "type" && Equals(parameter.Value, type));
        query.Parameters.Should().Contain(parameter => parameter.Name == "id0" && Equals(parameter.Value, firstId));
        query.Parameters.Should().Contain(parameter => parameter.Name == "id1" && Equals(parameter.Value, secondId));
    }

    private static bool ParameterValueEquals(object? actual, object? expected)
    {
        return actual is JValue jValue
            ? Equals(jValue.Value, expected)
            : Equals(actual, expected);
    }

    private static new IServiceProvider ServiceProvider<TService>(TService service) where TService : class
    {
        return new ServiceCollection()
            .AddSingleton(service)
            .BuildServiceProvider();
    }

    private sealed class RecordingMediator(Func<IRequest, object> responseFactory) : IMediator
    {
        public List<IRequest> Requests { get; } = [];

        public Task<OneOf.OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OneOf.OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
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
            Requests.Add(request);
            return Task.FromResult(((TResponse?)responseFactory(request), (Exception?)null));
        }
    }

    private static IEnumerable<string> FindMapperCallSites()
    {
        var srcRoot = FindDataHubSourceRoot();
        var regex = new Regex(@"DataHubQueryParameterMapper\.(ToDataServiceParameters|Combine)\b", RegexOptions.Compiled);

        return Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                var relativePath = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
                return regex.Matches(File.ReadAllText(file))
                    .Select(match => $"{relativePath}|{match.Groups[1].Value}");
            });
    }

    private static string FindDataHubSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            foreach (var projectRoot in new[]
            {
                Path.Combine(directory.FullName, "src", "Reimaginate.DataHub"),
                Path.Combine(directory.FullName, "framework", "Reimaginate.DataHub")
            })
            {
                if (File.Exists(Path.Combine(projectRoot, "Reimaginate.DataHub.csproj")))
                {
                    return projectRoot;
                }
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate Reimaginate.DataHub in a supported private or public repository layout.");
    }
}
