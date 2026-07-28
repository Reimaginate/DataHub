using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Reimaginate.DataHub.AspNetCore;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DataHubEndpointDiagnosticsTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task CreateErrorResult_should_return_structured_bad_request_for_diagnostic_exception()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateErrorResult_should_return_structured_bad_request_for_diagnostic_exception)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateErrorResult_should_return_structured_bad_request_for_diagnostic_exception))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var context = CreateHttpContext();

                    var result = DataHubEndpointDiagnostics.CreateErrorResult(
                        new DataHubInvalidRequestException("RequestType is required.", correlationId: "corr-1"),
                        context,
                        NullLoggerFactory.Instance.CreateLogger("test"));

                    await result.ExecuteAsync(context);

                    context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                    context.Response.Headers[DataHubEndpointDiagnostics.CorrelationIdHeaderName].Should().ContainSingle("corr-1");
                    var error = await ReadErrorResponseAsync(context);
                    error.Category.Should().Be(DataHubErrorCategory.InvalidRequest);
                    error.Message.Should().Be("RequestType is required.");
                    error.CorrelationId.Should().Be("corr-1");

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
    public async Task CreateErrorResult_should_not_leak_server_exception_message()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateErrorResult_should_not_leak_server_exception_message)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateErrorResult_should_not_leak_server_exception_message))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var context = CreateHttpContext();

                    var result = DataHubEndpointDiagnostics.CreateErrorResult(
                        new InvalidOperationException("database secret details"),
                        context,
                        NullLoggerFactory.Instance.CreateLogger("test"),
                        requestType: nameof(DataHubEndpointDiagnosticsTests),
                        correlationId: "corr-2");

                    await result.ExecuteAsync(context);

                    context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
                    var error = await ReadErrorResponseAsync(context);
                    error.Category.Should().Be(DataHubErrorCategory.ServerError);
                    error.Message.Should().Be("An unexpected DataHub server error occurred.");
                    error.Message.Should().NotContain("database secret details");
                    error.RequestType.Should().Be(nameof(DataHubEndpointDiagnosticsTests));

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
    public async Task CreateErrorResult_should_preserve_explicit_datahub_server_diagnostic_message()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateErrorResult_should_preserve_explicit_datahub_server_diagnostic_message)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateErrorResult_should_preserve_explicit_datahub_server_diagnostic_message))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var context = CreateHttpContext();

                    var result = DataHubEndpointDiagnostics.CreateErrorResult(
                        new DataHubException(
                            DataHubErrorCategory.ServerError,
                            "Patch pipeline did not return a result for every resolved reference owner.",
                            requestType: "ResolveResolutionPromisesRequest",
                            correlationId: "corr-datahub-server",
                            details:
                            [
                                new DataHubErrorDetail
                                {
                                    Field = "Owner",
                                    Code = "PatchResultMissing",
                                    Message = "OwnerType:owner-1"
                                }
                            ]),
                        context,
                        NullLoggerFactory.Instance.CreateLogger("test"));

                    await result.ExecuteAsync(context);

                    context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
                    var error = await ReadErrorResponseAsync(context);
                    error.Category.Should().Be(DataHubErrorCategory.ServerError);
                    error.Message.Should().Be("Patch pipeline did not return a result for every resolved reference owner.");
                    error.Details.Should().ContainSingle(detail =>
                        detail.Field == "Owner" &&
                        detail.Code == "PatchResultMissing" &&
                        detail.Message == "OwnerType:owner-1");

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
    public async Task CreateUnauthorizedResult_should_return_structured_unauthorized_response()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateUnauthorizedResult_should_return_structured_unauthorized_response)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateUnauthorizedResult_should_return_structured_unauthorized_response))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var context = CreateHttpContext();

                    var result = DataHubEndpointDiagnostics.CreateUnauthorizedResult(
                        context,
                        NullLoggerFactory.Instance.CreateLogger("test"),
                        correlationId: "corr-3");

                    await result.ExecuteAsync(context);

                    context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
                    var error = await ReadErrorResponseAsync(context);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);
                    error.CorrelationId.Should().Be("corr-3");

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
    public async Task CreateErrorResult_should_map_fluent_validation_failures_to_details()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateErrorResult_should_map_fluent_validation_failures_to_details)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateErrorResult_should_map_fluent_validation_failures_to_details))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var context = CreateHttpContext();
                    var validationException = new ValidationException(new[]
                    {
                        new ValidationFailure("EntityId", "EntityId is required.")
                        {
                            ErrorCode = "NotEmptyValidator"
                        }
                    });

                    var result = DataHubEndpointDiagnostics.CreateErrorResult(
                        validationException,
                        context,
                        NullLoggerFactory.Instance.CreateLogger("test"),
                        requestType: "GetDataHubEntityRequest",
                        correlationId: "corr-validation");

                    await result.ExecuteAsync(context);

                    context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                    var error = await ReadErrorResponseAsync(context);
                    error.Category.Should().Be(DataHubErrorCategory.ValidationFailed);
                    error.RequestType.Should().Be("GetDataHubEntityRequest");
                    error.Message.Should().Be("Request validation failed.");
                    error.Details.Should().ContainSingle(detail =>
                        detail.Field == "EntityId" &&
                        detail.Code == "NotEmptyValidator" &&
                        detail.Message == "EntityId is required.");

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
    public async Task CreateErrorResult_should_map_legacy_not_authorized_validation_message_to_unauthorized()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateErrorResult_should_map_legacy_not_authorized_validation_message_to_unauthorized)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateErrorResult_should_map_legacy_not_authorized_validation_message_to_unauthorized))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var context = CreateHttpContext();

                    var result = DataHubEndpointDiagnostics.CreateErrorResult(
                        new InvalidOperationException("Validation failure: Not Authorized"),
                        context,
                        NullLoggerFactory.Instance.CreateLogger("test"),
                        requestType: "DeleteDuplicateRequest",
                        correlationId: "corr-unauthorized");

                    await result.ExecuteAsync(context);

                    context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
                    var error = await ReadErrorResponseAsync(context);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);
                    error.Message.Should().Be("Validation failure: Not Authorized");
                    error.RequestType.Should().Be("DeleteDuplicateRequest");

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
    public async Task CreateErrorResult_should_generate_correlation_id_when_not_supplied()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateErrorResult_should_generate_correlation_id_when_not_supplied)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateErrorResult_should_generate_correlation_id_when_not_supplied))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var context = CreateHttpContext();

                    var result = DataHubEndpointDiagnostics.CreateErrorResult(
                        new DataHubInvalidRequestException("Request body is required."),
                        context,
                        NullLoggerFactory.Instance.CreateLogger("test"));

                    await result.ExecuteAsync(context);

                    var error = await ReadErrorResponseAsync(context);
                    error.CorrelationId.Should().NotBeNullOrWhiteSpace();
                    context.Response.Headers[DataHubEndpointDiagnostics.CorrelationIdHeaderName].Should().ContainSingle(error.CorrelationId);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static async Task<DataHubErrorResponse> ReadErrorResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return JsonConvert.DeserializeObject<DataHubErrorResponse>(await reader.ReadToEndAsync())!;
    }
}
