using FluentAssertions;
using Newtonsoft.Json;
using Reimaginate.DataHub.Requests.External.CLI.DeserializeCliRequest;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DeserializeCliRequestTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task HandleAsync_should_deserialize_known_cli_request_and_apply_correlation_id()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_deserialize_known_cli_request_and_apply_correlation_id)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_deserialize_known_cli_request_and_apply_correlation_id))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeCliRequestRequestHandler();

                    var result = await handler.HandleAsync(new DeserializeCliRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = nameof(GetDataHubEntityTypeCountsRequest),
                            CorrelationId = "corr-cli-1",
                            Data = JsonConvert.SerializeObject(new
                            {
                                WhereClause = "x.entityType = 'Contact'"
                            })
                        }
                    }, CancellationToken.None);

                    var request = result.Should().BeOfType<GetDataHubEntityTypeCountsRequest>().Subject;
                    request.WhereClause.Should().Be("x.entityType = 'Contact'");
                    request.CorrelationId.Should().Be("corr-cli-1");

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
    public async Task HandleAsync_should_apply_trace_options_from_cli_envelope()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_apply_trace_options_from_cli_envelope)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_apply_trace_options_from_cli_envelope))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeCliRequestRequestHandler();
                    var traceOptions = new DataHubTraceOptions
                    {
                        Enabled = true,
                        IncludeRequest = false,
                        IncludeResponse = true,
                        Reason = "cli investigation",
                        ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15)
                    };

                    var result = await handler.HandleAsync(new DeserializeCliRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = nameof(GetDataHubEntityTypeCountsRequest),
                            CorrelationId = "corr-cli-trace",
                            TraceOptions = traceOptions,
                            Data = JsonConvert.SerializeObject(new
                            {
                                WhereClause = "x.entityType = 'Contact'"
                            })
                        }
                    }, CancellationToken.None);

                    var request = result.Should().BeOfType<GetDataHubEntityTypeCountsRequest>().Subject;
                    request.CorrelationId.Should().Be("corr-cli-trace");
                    request.TraceOptions.Should().BeEquivalentTo(traceOptions);

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
    public async Task HandleAsync_should_throw_diagnostic_exception_for_missing_envelope()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_envelope)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_envelope))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeCliRequestRequestHandler();

                    var act = () => handler.HandleAsync(new DeserializeCliRequestRequest(), CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubInvalidRequestException>();
                    exception.Which.Message.Should().Be("Request envelope is required.");
                    exception.Which.Category.Should().Be(DataHubErrorCategory.InvalidRequest);

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
    public async Task HandleAsync_should_throw_diagnostic_exception_for_missing_request_type()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_request_type)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_request_type))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeCliRequestRequestHandler();

                    var act = () => handler.HandleAsync(new DeserializeCliRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            CorrelationId = "corr-cli-2",
                            Data = "{}"
                        }
                    }, CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubInvalidRequestException>();
                    exception.Which.Message.Should().Be("RequestType is required.");
                    exception.Which.CorrelationId.Should().Be("corr-cli-2");
                    exception.Which.Category.Should().Be(DataHubErrorCategory.InvalidRequest);

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
    public async Task HandleAsync_should_throw_for_invalid_request_type()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_throw_for_invalid_request_type)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_throw_for_invalid_request_type))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeCliRequestRequestHandler();

                    var act = () => handler.HandleAsync(new DeserializeCliRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = "NotARealCliRequest",
                            CorrelationId = "corr-cli-3",
                            Data = "{}"
                        }
                    }, CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubInvalidRequestException>();
                    exception.Which.Message.Should().Be("Invalid request type 'NotARealCliRequest'.");
                    exception.Which.RequestType.Should().Be("NotARealCliRequest");
                    exception.Which.CorrelationId.Should().Be("corr-cli-3");
                    exception.Which.Category.Should().Be(DataHubErrorCategory.InvalidRequest);

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
    public async Task HandleAsync_should_throw_diagnostic_exception_for_malformed_data()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_malformed_data)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_malformed_data))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeCliRequestRequestHandler();

                    var act = () => handler.HandleAsync(new DeserializeCliRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = nameof(DeleteDataHubEntitiesRequest),
                            CorrelationId = "corr-cli-4",
                            Data = JsonConvert.SerializeObject(new
                            {
                                IncludeTrackingEntries = "not-a-bool"
                            })
                        }
                    }, CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubDeserializationException>();
                    exception.Which.Message.Should().Be("Request Data could not be deserialized.");
                    exception.Which.RequestType.Should().Be(nameof(DeleteDataHubEntitiesRequest));
                    exception.Which.CorrelationId.Should().Be("corr-cli-4");
                    exception.Which.Category.Should().Be(DataHubErrorCategory.DeserializationFailed);
                    var detail = exception.Which.Details.Should().ContainSingle().Subject;
                    detail.Code.Should().BeOneOf(nameof(JsonReaderException), nameof(JsonSerializationException));
                    detail.Message.Should().Contain(nameof(DeleteDataHubEntitiesRequest.IncludeTrackingEntries));

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }
}
