using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reimaginate.DataHub.Config;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.Requests.Internal.GetLogs;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.Requests.Internal.RecordSyncEventsAgainstDataHubEntities;
using Reimaginate.DataHub.Requests.External.Client.ReserveAutoNumbers;
using Reimaginate.DataHub.Services.AutoNumbers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using GeneratedMediator = Reimaginate.DataHub.Mediator;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DataHubDependencyInjectionTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task AddDataHub_should_register_generated_mediator_as_IMediator()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(AddDataHub_should_register_generated_mediator_as_IMediator)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(AddDataHub_should_register_generated_mediator_as_IMediator))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var services = new ServiceCollection();

                    services.AddDataHub(options => options
                        .WithAppSettingsConfig(CreateConfig())
                        .WithDatabase(database => database.UseInMemoryDatabase())
                        .WithProcessingLockOptions(locks => locks.UseInMemoryRepository()));

                    services.Should().Contain(descriptor =>
                        descriptor.ServiceType == typeof(IMediator) &&
                        descriptor.ImplementationType == typeof(GeneratedMediator) &&
                        descriptor.Lifetime == ServiceLifetime.Transient);

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
    public async Task AddDataHub_should_keep_representative_generated_mediator_gap_handlers_registered()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(AddDataHub_should_keep_representative_generated_mediator_gap_handlers_registered)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(AddDataHub_should_keep_representative_generated_mediator_gap_handlers_registered))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var services = new ServiceCollection();

                    services.AddDataHub(options => options
                        .WithAppSettingsConfig(CreateConfig())
                        .WithDatabase(database => database.UseInMemoryDatabase())
                        .WithProcessingLockOptions(locks => locks.UseInMemoryRepository()));

                    ShouldContainHandler<UpsertCosmosDocumentsCommand<ResolutionPromise>, UpsertCosmosDocumentsResponse<ResolutionPromise>, UpsertCosmosDocumentsCommandHandler<ResolutionPromise>>(services);
                    ShouldContainHandler<GetCosmosDocumentsQuery<ResolutionPromise>, PagedResults<ResolutionPromise>, GetCosmosDocumentsQueryHandler<ResolutionPromise>>(services);
                    ShouldContainHandler<DeleteCosmosDocumentsCommand<ResolutionPromise>, DeleteCosmosDocumentsResponse<ResolutionPromise>, DeleteCosmosDocumentsCommandHandler<ResolutionPromise>>(services);
                    ShouldContainHandler<LogEventsRequest<Alert>, LogEventsResponse, LogEventsRequestHandler<Alert>>(services);
                    ShouldContainHandler<LogSyncEventsRequest<MergeFailure>, LogEventsResponse, LogSyncEventsRequestHandler<MergeFailure>>(services);
                    ShouldContainHandler<RecordSyncEventsAgainstDataHubEntitiesRequest<MergeFailure>, NullResponse, RecordSyncEventsAgainstDataHubEntitiesRequestHandler<MergeFailure>>(services);
                    ShouldContainHandler<GetLogsRequest<MergeFailure>, GetLogsResponse, GetLogsRequestHandler<MergeFailure>>(services);
                    ShouldContainHandler<ReserveAutoNumbersRequest, ReserveAutoNumbersResponse, ReserveAutoNumbersRequestHandler>(services);
                    ShouldContainHandler<UpsertCosmosDocumentsCommand<AutoNumberSequence>, UpsertCosmosDocumentsResponse<AutoNumberSequence>, UpsertCosmosDocumentsCommandHandler<AutoNumberSequence>>(services);
                    services.Should().Contain(descriptor =>
                        descriptor.ServiceType == typeof(IAutoNumberService) &&
                        descriptor.ImplementationType == typeof(AutoNumberService) &&
                        descriptor.Lifetime == ServiceLifetime.Transient);

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
    public async Task AddDataHub_should_bind_trace_query_options_from_canonical_observability_section()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(AddDataHub_should_bind_trace_query_options_from_canonical_observability_section)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(AddDataHub_should_bind_trace_query_options_from_canonical_observability_section))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var configuration = CreateConfig(new Dictionary<string, string?>
                    {
                        ["DataHub:Observability:LogsWorkspaceId"] = "workspace-1",
                        ["DataHub:Observability:LogsResourceId"] = "/subscriptions/sub-1/resourceGroups/rg-1/providers/microsoft.insights/components/app-1",
                        ["DataHub:Observability:DefaultLookbackHours"] = "12",
                        ["DataHub:Observability:MaxLookbackHours"] = "48",
                        ["DataHub:Observability:DefaultResultCount"] = "250",
                        ["DataHub:Observability:MaxResultCount"] = "750"
                    });

                    var services = new ServiceCollection();

                    services.AddDataHub(options => options
                        .WithAppSettingsConfig(configuration, "DataHub")
                        .WithDatabase(database => database.UseInMemoryDatabase())
                        .WithProcessingLockOptions(locks => locks.UseInMemoryRepository()));

                    using var provider = services.BuildServiceProvider();
                    var traceQueryOptions = provider.GetRequiredService<IOptions<DataHubTraceQueryOptions>>().Value;

                    traceQueryOptions.LogsWorkspaceId.Should().Be("workspace-1");
                    traceQueryOptions.LogsResourceId.Should().Be("/subscriptions/sub-1/resourceGroups/rg-1/providers/microsoft.insights/components/app-1");
                    traceQueryOptions.DefaultLookbackHours.Should().Be(12);
                    traceQueryOptions.MaxLookbackHours.Should().Be(48);
                    traceQueryOptions.DefaultResultCount.Should().Be(250);
                    traceQueryOptions.MaxResultCount.Should().Be(750);
                    configuration["DataHub:DataHub:Observability:LogsWorkspaceId"].Should().BeNull();

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static void ShouldContainHandler<TRequest, TResponse, THandler>(IServiceCollection services)
        where TRequest : IRequest<TResponse>
        where THandler : IHandler<TRequest, TResponse>
    {
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHandler<TRequest, TResponse>) &&
            descriptor.ImplementationType == typeof(THandler) &&
            descriptor.Lifetime == ServiceLifetime.Transient);
    }

    private static IConfiguration CreateConfig(Dictionary<string, string?>? config = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? new Dictionary<string, string?>())
            .Build();
    }
}
