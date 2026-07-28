using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using OneOf;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.AspNetCore;
using Reimaginate.DataHub.AspNetCore.Observability;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.Requests.External.CLI.DeserializeCliRequest;
using Reimaginate.DataHub.Requests.External.Client.DeserializeClientRequest;
using Reimaginate.DataHub.Requests.Internal.GetUser;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.Mediator;
using System.IdentityModel.Tokens.Jwt;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DataHubEndpointRouteBuilderExtensionsTests : ScenarioUnitTestBase
{
    private const string SignedJwtKey = "DataHub client endpoint tests signing key 2026";

    [Fact]
    public async Task Client_endpoint_should_return_structured_unauthorized_when_key_is_missing()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_return_structured_unauthorized_when_key_is_missing)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_return_structured_unauthorized_when_key_is_missing))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.AuthorizeAsync = (request, _) => Task.FromResult(request.Headers["x-functions-key"] == "expected");
                    }));

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest())
                    };
                    request.Headers.TryAddWithoutValidation(DataHubEndpointDiagnostics.CorrelationIdHeaderName, "corr-client-401");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                    response.Headers.GetValues(DataHubEndpointDiagnostics.CorrelationIdHeaderName).Should().ContainSingle("corr-client-401");
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);
                    error.CorrelationId.Should().Be("corr-client-401");

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
    public async Task Client_endpoint_should_return_structured_bad_request_for_empty_body()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_return_structured_bad_request_for_empty_body)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_return_structured_bad_request_for_empty_body))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(_ => { }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.AuthorizeAsync = (_, _) => Task.FromResult(true);
                    }));

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = new StringContent("", Encoding.UTF8, "application/json")
                    };
                    request.Headers.TryAddWithoutValidation(DataHubEndpointDiagnostics.CorrelationIdHeaderName, "corr-empty");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.InvalidRequest);
                    error.Message.Should().Be("Request body is required.");
                    error.CorrelationId.Should().Be("corr-empty");

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
    public async Task Client_endpoint_should_return_structured_bad_request_for_malformed_envelope()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_return_structured_bad_request_for_malformed_envelope)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_return_structured_bad_request_for_malformed_envelope))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(_ => { }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.AuthorizeAsync = (_, _) => Task.FromResult(true);
                    }));

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = new StringContent("{", Encoding.UTF8, "application/json")
                    };
                    request.Headers.TryAddWithoutValidation(DataHubEndpointDiagnostics.CorrelationIdHeaderName, "corr-json");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.DeserializationFailed);
                    error.Message.Should().Be("Request envelope could not be deserialized.");
                    error.CorrelationId.Should().Be("corr-json");

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
    public async Task Client_endpoint_should_return_structured_bad_request_for_unknown_request_type()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_return_structured_bad_request_for_unknown_request_type)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_return_structured_bad_request_for_unknown_request_type))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendException = new DataHubInvalidRequestException(
                            "Invalid request type 'Nope'.",
                            "Nope",
                            "corr-unknown");
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.AuthorizeAsync = (_, _) => Task.FromResult(true);
                    }));

                    var client = app.GetTestClient();
                    var response = await client.PostAsync("/api/Client", JsonContent(new SerializedRequest
                    {
                        RequestType = "Nope",
                        CorrelationId = "corr-unknown",
                        Data = "{}"
                    }));

                    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.InvalidRequest);
                    error.RequestType.Should().Be("Nope");
                    error.CorrelationId.Should().Be("corr-unknown");

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
    public async Task Client_endpoint_should_deserialize_then_runtime_dispatch_valid_request()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_deserialize_then_runtime_dispatch_valid_request)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_deserialize_then_runtime_dispatch_valid_request))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var deserializedRequest = new EndpointClientRequest();
                    RecordingMediator? capturedMediator = null;
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        capturedMediator = mediator;
                        mediator.TrySendResponse = deserializedRequest;
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.AuthorizeAsync = (_, _) => Task.FromResult(true);
                    }));

                    var client = app.GetTestClient();
                    var response = await client.PostAsync("/api/Client", JsonContent(new SerializedRequest
                    {
                        RequestType = nameof(EndpointClientRequest),
                        CorrelationId = "corr-valid-client",
                        Data = "{}"
                    }));

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    var body = JsonConvert.DeserializeObject<EndpointTestResponse>(await response.Content.ReadAsStringAsync());
                    body!.Success.Should().BeTrue();
                    capturedMediator.Should().NotBeNull();
                    capturedMediator!.TrySendRequests.Should().ContainSingle()
                        .Which.Should().BeOfType<DeserializeClientRequestRequest>()
                        .Which.SerializedRequest.CorrelationId.Should().Be("corr-valid-client");
                    capturedMediator.RuntimeSendRequests.Should().ContainSingle().Which.Should().BeSameAs(deserializedRequest);

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
    public async Task Client_endpoint_should_tag_success_activity_without_payload_by_default()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_tag_success_activity_without_payload_by_default)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_tag_success_activity_without_payload_by_default))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    using var activityCapture = new ActivityCapture();
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponse = new EndpointClientRequest();
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.AuthorizeAsync = (_, _) => Task.FromResult(true);
                    }));

                    var client = app.GetTestClient();
                    var response = await client.PostAsync("/api/Client", JsonContent(new SerializedRequest
                    {
                        RequestType = nameof(EndpointClientRequest),
                        CorrelationId = "corr-client-observable",
                        Data = """{"clientSecret":"must-not-emit"}"""
                    }));

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    var activity = FindDataHubActivity(activityCapture, "corr-client-observable");
                    activity.GetTagItem("datahub.request_type").Should().Be(nameof(EndpointClientRequest));
                    activity.GetTagItem("datahub.endpoint").Should().Be("client");
                    activity.GetTagItem("datahub.request.payload").Should().BeNull();
                    activity.GetTagItem("datahub.response.payload").Should().BeNull();

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
    public async Task Client_endpoint_should_emit_diagnostic_payload_tags_only_for_matching_filter()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_emit_diagnostic_payload_tags_only_for_matching_filter)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_emit_diagnostic_payload_tags_only_for_matching_filter))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    using var activityCapture = new ActivityCapture();
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponse = new EndpointClientRequest();
                        mediator.SendResponse = new EndpointSecretResponse
                        {
                            Success = true,
                            AccessToken = "secret-token",
                            Payload = "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz"
                        };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.AuthorizeAsync = (_, _) => Task.FromResult(true);
                    }), services =>
                    {
                        services.AddSingleton(Options.Create(new DataHubObservabilityOptions
                        {
                            Profile = DataHubObservabilityProfile.Diagnostic,
                            DiagnosticCorrelationIds = ["corr-diagnostic-endpoint"],
                            DiagnosticExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5),
                            EnableRequestPayloadTracing = true,
                            EnableResponsePayloadTracing = true,
                            MaxTelemetryPayloadCharacters = 120
                        }));
                    });

                    var client = app.GetTestClient();
                    var matchingResponse = await client.PostAsync("/api/Client", JsonContent(new SerializedRequest
                    {
                        RequestType = nameof(EndpointClientRequest),
                        CorrelationId = "corr-diagnostic-endpoint",
                        Data = """
                        {
                          "clientSecret": "super-secret",
                          "payload": "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz"
                        }
                        """
                    }));
                    var nonMatchingResponse = await client.PostAsync("/api/Client", JsonContent(new SerializedRequest
                    {
                        RequestType = nameof(EndpointClientRequest),
                        CorrelationId = "corr-other-endpoint",
                        Data = """{"clientSecret":"must-not-emit"}"""
                    }));

                    matchingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
                    nonMatchingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

                    var matchingActivity = FindDataHubActivity(activityCapture, "corr-diagnostic-endpoint");
                    matchingActivity.GetTagItem("datahub.request.payload").Should().BeOfType<string>().Which.Should().Contain("[redacted]");
                    matchingActivity.GetTagItem("datahub.request.payload")!.ToString().Should().NotContain("super-secret");
                    matchingActivity.GetTagItem("datahub.response.payload").Should().BeOfType<string>().Which.Should().Contain("[redacted]");
                    matchingActivity.GetTagItem("datahub.response.payload")!.ToString().Should().NotContain("secret-token");

                    var nonMatchingActivity = FindDataHubActivity(activityCapture, "corr-other-endpoint");
                    nonMatchingActivity.GetTagItem("datahub.request.payload").Should().BeNull();
                    nonMatchingActivity.GetTagItem("datahub.response.payload").Should().BeNull();

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
    public async Task Client_endpoint_should_tag_failed_activity_and_emit_failure_metric()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_tag_failed_activity_and_emit_failure_metric)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_tag_failed_activity_and_emit_failure_metric))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    using var activityCapture = new ActivityCapture();
                    using var metricCapture = new MetricCapture("datahub.request.failure.count");
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponse = new EndpointClientRequest();
                        mediator.SendException = new DataHubException(
                            DataHubErrorCategory.ServerError,
                            "runtime failed",
                            nameof(EndpointClientRequest),
                            "corr-client-failed");
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.AuthorizeAsync = (_, _) => Task.FromResult(true);
                    }));

                    var client = app.GetTestClient();
                    var response = await client.PostAsync("/api/Client", JsonContent(new SerializedRequest
                    {
                        RequestType = nameof(EndpointClientRequest),
                        CorrelationId = "corr-client-failed",
                        Data = "{}"
                    }));

                    response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                    var activity = FindDataHubActivity(activityCapture, "corr-client-failed");
                    activity.GetTagItem("datahub.error_id").Should().NotBeNull();
                    activity.GetTagItem("datahub.error_category").Should().Be(DataHubErrorCategory.ServerError);
                    activity.GetTagItem("datahub.http_status_code").Should().Be(StatusCodes.Status500InternalServerError);
                    metricCapture.Measurements.Should().Contain(metric =>
                        metric.Name == "datahub.request.failure.count" &&
                        Equals(metric.Tags["request.type"], nameof(EndpointClientRequest)) &&
                        Equals(metric.Tags["error.category"], DataHubErrorCategory.ServerError));

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
    public async Task Client_endpoint_should_return_structured_unauthorized_when_bearer_token_is_missing()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_return_structured_unauthorized_when_bearer_token_is_missing)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_return_structured_unauthorized_when_bearer_token_is_missing))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponse = new EndpointClientRequest();
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.UseAzureAdBearerAuthorization();
                    }), AddTestBearerAuthorizationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest())
                    };
                    request.Headers.TryAddWithoutValidation(DataHubEndpointDiagnostics.CorrelationIdHeaderName, "corr-bearer-missing");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);
                    error.CorrelationId.Should().Be("corr-bearer-missing");

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
    public async Task Client_endpoint_should_accept_allow_listed_bearer_client_id()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_accept_allow_listed_bearer_client_id)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_accept_allow_listed_bearer_client_id))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var deserializedRequest = new EndpointClientRequest();
                    RecordingMediator? capturedMediator = null;
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        capturedMediator = mediator;
                        mediator.TrySendResponse = deserializedRequest;
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.UseAzureAdBearerAuthorization();
                    }), AddTestBearerAuthorizationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest
                        {
                            RequestType = nameof(EndpointClientRequest),
                            CorrelationId = "corr-bearer-client",
                            Data = "{}"
                        })
                    };
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer allowed-client");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    capturedMediator!.RuntimeSendRequests.Should().ContainSingle().Which.Should().BeSameAs(deserializedRequest);

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
    public async Task Client_endpoint_should_accept_allow_listed_bearer_appid_claim()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_accept_allow_listed_bearer_appid_claim)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_accept_allow_listed_bearer_appid_claim))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var deserializedRequest = new EndpointClientRequest();
                    RecordingMediator? capturedMediator = null;
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        capturedMediator = mediator;
                        mediator.TrySendResponse = deserializedRequest;
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.UseAzureAdBearerAuthorization();
                    }), AddTestBearerAuthorizationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest
                        {
                            RequestType = nameof(EndpointClientRequest),
                            CorrelationId = "corr-bearer-appid",
                            Data = "{}"
                        })
                    };
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer allowed-appid");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    capturedMediator!.RuntimeSendRequests.Should().ContainSingle().Which.Should().BeSameAs(deserializedRequest);

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
    public async Task Client_endpoint_should_accept_allow_listed_bearer_object_id()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_accept_allow_listed_bearer_object_id)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_accept_allow_listed_bearer_object_id))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var deserializedRequest = new EndpointClientRequest();
                    RecordingMediator? capturedMediator = null;
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        capturedMediator = mediator;
                        mediator.TrySendResponse = deserializedRequest;
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.UseAzureAdBearerAuthorization();
                    }), AddTestBearerAuthorizationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest
                        {
                            RequestType = nameof(EndpointClientRequest),
                            CorrelationId = "corr-bearer-object",
                            Data = "{}"
                        })
                    };
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer allowed-object");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    capturedMediator!.RuntimeSendRequests.Should().ContainSingle().Which.Should().BeSameAs(deserializedRequest);

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
    public async Task Client_endpoint_should_reject_non_allow_listed_bearer_client_id()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_reject_non_allow_listed_bearer_client_id)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_reject_non_allow_listed_bearer_client_id))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponse = new EndpointClientRequest();
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.UseAzureAdBearerAuthorization();
                    }), AddTestBearerAuthorizationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest())
                    };
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer denied-client");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);

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
    public async Task Client_endpoint_should_reject_delegated_bearer_token()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_reject_delegated_bearer_token)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_reject_delegated_bearer_token))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponse = new EndpointClientRequest();
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.UseAzureAdBearerAuthorization();
                    }), AddTestBearerAuthorizationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest())
                    };
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer delegated-client");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);

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
    public async Task Client_endpoint_should_accept_locally_signed_jwt_with_valid_issuer_audience_and_allow_listed_client_id()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_accept_locally_signed_jwt_with_valid_issuer_audience_and_allow_listed_client_id)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_accept_locally_signed_jwt_with_valid_issuer_audience_and_allow_listed_client_id))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var deserializedRequest = new EndpointClientRequest();
                    RecordingMediator? capturedMediator = null;
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        capturedMediator = mediator;
                        mediator.TrySendResponse = deserializedRequest;
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.UseAzureAdBearerAuthorization();
                    }), AddSignedJwtBearerAuthorizationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest
                        {
                            RequestType = nameof(EndpointClientRequest),
                            CorrelationId = "corr-signed-valid",
                            Data = "{}"
                        })
                    };
                    request.Headers.TryAddWithoutValidation(
                        "Authorization",
                        $"Bearer {CreateSignedJwt("https://login.microsoftonline.com/tenant-1/v2.0", "api://datahub-api", new Claim("azp", "allowed-client-id"))}");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    capturedMediator!.RuntimeSendRequests.Should().ContainSingle().Which.Should().BeSameAs(deserializedRequest);

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
    public async Task Client_endpoint_should_reject_locally_signed_jwt_with_wrong_audience()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_reject_locally_signed_jwt_with_wrong_audience)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_reject_locally_signed_jwt_with_wrong_audience))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponse = new EndpointClientRequest();
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.UseAzureAdBearerAuthorization();
                    }), AddSignedJwtBearerAuthorizationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest())
                    };
                    request.Headers.TryAddWithoutValidation(
                        "Authorization",
                        $"Bearer {CreateSignedJwt("https://login.microsoftonline.com/tenant-1/v2.0", "api://other-api", new Claim("azp", "allowed-client-id"))}");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);

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
    public async Task Client_endpoint_should_reject_locally_signed_jwt_with_wrong_issuer()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_reject_locally_signed_jwt_with_wrong_issuer)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_reject_locally_signed_jwt_with_wrong_issuer))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponse = new EndpointClientRequest();
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.UseAzureAdBearerAuthorization();
                    }), AddSignedJwtBearerAuthorizationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Client")
                    {
                        Content = JsonContent(new SerializedRequest())
                    };
                    request.Headers.TryAddWithoutValidation(
                        "Authorization",
                        $"Bearer {CreateSignedJwt("https://login.microsoftonline.com/other-tenant/v2.0", "api://datahub-api", new Claim("azp", "allowed-client-id"))}");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Theory]
    [InlineData(null, "api://datahub-api", new string[] { "allowed-client-id" }, new string[] { "allowed-object-id" }, "TenantId")]
    [InlineData("tenant-1", null, new string[] { "allowed-client-id" }, new string[] { "allowed-object-id" }, "Audience")]
    [InlineData("tenant-1", "api://datahub-api", new string[0], new string[0], "AllowedClientIds")]
    public async Task AddDataHubClientAzureAdAuthorization_should_reject_incomplete_configuration(
        string? tenantId,
        string? audience,
        string[] allowedClientIds,
        string[] allowedObjectIds,
        string expectedOptionName)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(AddDataHubClientAzureAdAuthorization_should_reject_incomplete_configuration)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(AddDataHubClientAzureAdAuthorization_should_reject_incomplete_configuration))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var services = new ServiceCollection();

                    var act = () => services.AddDataHubClientAzureAdAuthorization(options =>
                    {
                        options.TenantId = tenantId;
                        options.Audience = audience;
                        options.AllowedClientIds = allowedClientIds;
                        options.AllowedObjectIds = allowedObjectIds;
                    });

                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage($"*{expectedOptionName}*");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer cli-app-only")]
    [InlineData("Bearer cli-wrong-scope")]
    public async Task Cli_endpoint_should_reject_missing_app_only_or_wrong_scope_token(string? authorization)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Cli_endpoint_should_reject_missing_app_only_or_wrong_scope_token)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Cli_endpoint_should_reject_missing_app_only_or_wrong_scope_token))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponseFactory = request => request switch
                        {
                            GetUserRequest => new GetUserResponse
                            {
                                Success = true,
                                Result = new User { id = "user-1", TenantId = "tenant-1", EntraObjectId = "object-1" }
                            },
                            DeserializeCliRequestRequest => new EndpointCliRequest(),
                            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
                        };
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubCliEndpoint("/api/CLI", options =>
                    {
                        options.UseAzureAdCliAuthentication();
                    }), AddTestCliBearerAuthenticationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/CLI")
                    {
                        Content = JsonContent(new SerializedRequest
                        {
                            RequestType = nameof(EndpointCliRequest),
                            CorrelationId = "corr-cli-auth",
                            Data = "{}"
                        })
                    };
                    if (!string.IsNullOrWhiteSpace(authorization))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", authorization);
                    }

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);

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
    public async Task Cli_endpoint_should_accept_valid_delegated_token_and_resolve_registered_user()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Cli_endpoint_should_accept_valid_delegated_token_and_resolve_registered_user)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Cli_endpoint_should_accept_valid_delegated_token_and_resolve_registered_user))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var deserializedRequest = new EndpointCliRequest();
                    var registeredUser = new User
                    {
                        id = "user-1",
                        TenantId = "tenant-1",
                        EntraObjectId = "object-1",
                        UPN = "admin@contoso.test"
                    };
                    RecordingMediator? capturedMediator = null;
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        capturedMediator = mediator;
                        mediator.TrySendResponseFactory = request => request switch
                        {
                            GetUserRequest getUserRequest => getUserRequest.TenantId == "tenant-1" &&
                                                             getUserRequest.EntraObjectId == "object-1" &&
                                                             getUserRequest.UserId == "admin@contoso.test"
                                ? new GetUserResponse { Success = true, Result = registeredUser }
                                : new GetUserResponse { Success = false, FailureReason = "NOT_FOUND" },
                            DeserializeCliRequestRequest => deserializedRequest,
                            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
                        };
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubCliEndpoint("/api/CLI", options =>
                    {
                        options.UseAzureAdCliAuthentication();
                    }), AddTestCliBearerAuthenticationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/CLI")
                    {
                        Content = JsonContent(new SerializedRequest
                        {
                            RequestType = nameof(EndpointCliRequest),
                            CorrelationId = "corr-cli-valid-auth",
                            Data = "{}"
                        })
                    };
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer cli-valid");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    deserializedRequest.User.Should().BeSameAs(registeredUser);
                    capturedMediator!.TrySendRequests.Should().Contain(request => request is GetUserRequest);

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
    public async Task Cli_endpoint_should_attach_effective_permissions_to_authenticated_user()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Cli_endpoint_should_attach_effective_permissions_to_authenticated_user)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Cli_endpoint_should_attach_effective_permissions_to_authenticated_user))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var deserializedRequest = new EndpointCliRequest();
                    var registeredUser = new User
                    {
                        id = "user-1",
                        TenantId = "tenant-1",
                        EntraObjectId = "object-1",
                        UPN = "admin@contoso.test",
                        Roles = ["operators"]
                    };

                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponseFactory = request => request switch
                        {
                            GetUserRequest => new GetUserResponse { Success = true, Result = registeredUser },
                            DeserializeCliRequestRequest => deserializedRequest,
                            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
                        };
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubCliEndpoint("/api/CLI", options =>
                    {
                        options.UseAzureAdCliAuthentication();
                    }), services =>
                    {
                        AddTestCliBearerAuthenticationServices(services);
                        services.AddSingleton<IDataHubAuthorizationService>(new ResolvingAuthorizationService([DataHubPermissions.QueryRoles]));
                    });

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/CLI")
                    {
                        Content = JsonContent(new SerializedRequest
                        {
                            RequestType = nameof(EndpointCliRequest),
                            CorrelationId = "corr-cli-permissions",
                            Data = "{}"
                        })
                    };
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer cli-valid");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    deserializedRequest.User.EffectivePermissions.Should().Equal(DataHubPermissions.QueryRoles);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Cli_endpoint_should_reject_disabled_or_unregistered_user(bool userFound, bool disabled)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Cli_endpoint_should_reject_disabled_or_unregistered_user)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Cli_endpoint_should_reject_disabled_or_unregistered_user))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponseFactory = request => request switch
                        {
                            GetUserRequest => userFound
                                ? new GetUserResponse
                                {
                                    Success = true,
                                    Result = new User
                                    {
                                        id = "user-1",
                                        TenantId = "tenant-1",
                                        EntraObjectId = "object-1",
                                        Disabled = disabled
                                    }
                                }
                                : new GetUserResponse { Success = false, FailureReason = "NOT_FOUND" },
                            DeserializeCliRequestRequest => new EndpointCliRequest(),
                            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
                        };
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubCliEndpoint("/api/CLI", options =>
                    {
                        options.UseAzureAdCliAuthentication();
                    }), AddTestCliBearerAuthenticationServices);

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/CLI")
                    {
                        Content = JsonContent(new SerializedRequest
                        {
                            RequestType = nameof(EndpointCliRequest),
                            CorrelationId = "corr-cli-denied-user",
                            Data = "{}"
                        })
                    };
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer cli-valid");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Theory]
    [InlineData(null, "api://datahub-api", "datahub_cli", "TenantId")]
    [InlineData("tenant-1", null, "datahub_cli", "Audience")]
    [InlineData("tenant-1", "api://datahub-api", null, "RequiredScope")]
    public async Task AddDataHubCliAzureAdAuthentication_should_reject_incomplete_configuration(
        string? tenantId,
        string? audience,
        string? requiredScope,
        string expectedOptionName)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(AddDataHubCliAzureAdAuthentication_should_reject_incomplete_configuration)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(AddDataHubCliAzureAdAuthentication_should_reject_incomplete_configuration))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var services = new ServiceCollection();

                    var act = () => services.AddDataHubCliAzureAdAuthentication(options =>
                    {
                        options.TenantId = tenantId;
                        options.Audience = audience;
                        options.RequiredScope = requiredScope;
                    });

                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage($"*{expectedOptionName}*");

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
    public async Task Client_endpoint_should_return_safe_server_error_when_runtime_dispatch_fails()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Client_endpoint_should_return_safe_server_error_when_runtime_dispatch_fails)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Client_endpoint_should_return_safe_server_error_when_runtime_dispatch_fails))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        mediator.TrySendResponse = new EndpointClientRequest();
                        mediator.SendException = new InvalidOperationException("secret storage details");
                    }, app => app.MapDataHubClientEndpoint("/api/Client", options =>
                    {
                        options.AuthorizeAsync = (_, _) => Task.FromResult(true);
                    }));

                    var client = app.GetTestClient();
                    var response = await client.PostAsync("/api/Client", JsonContent(new SerializedRequest
                    {
                        RequestType = nameof(EndpointClientRequest),
                        CorrelationId = "corr-server",
                        Data = "{}"
                    }));

                    response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.ServerError);
                    error.RequestType.Should().Be(nameof(EndpointClientRequest));
                    error.CorrelationId.Should().Be("corr-server");
                    error.Message.Should().Be("An unexpected DataHub server error occurred.");
                    error.Message.Should().NotContain("secret storage details");

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
    public async Task Cli_endpoint_should_return_structured_unauthorized_when_authentication_returns_no_user()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Cli_endpoint_should_return_structured_unauthorized_when_authentication_returns_no_user)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Cli_endpoint_should_return_structured_unauthorized_when_authentication_returns_no_user))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(_ => { }, app => app.MapDataHubCliEndpoint("/api/CLI", options =>
                    {
                        options.AuthenticateAsync = (_, _) => Task.FromResult<User>(null!);
                    }));

                    var client = app.GetTestClient();
                    var response = await client.PostAsync("/api/CLI", JsonContent(new SerializedRequest()));

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.Unauthorized);

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
    public async Task Cli_endpoint_should_return_structured_bad_request_for_authenticated_malformed_envelope()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Cli_endpoint_should_return_structured_bad_request_for_authenticated_malformed_envelope)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Cli_endpoint_should_return_structured_bad_request_for_authenticated_malformed_envelope))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(_ => { }, app => app.MapDataHubCliEndpoint("/api/CLI", options =>
                    {
                        options.AuthenticateAsync = (_, _) => Task.FromResult(new User { id = "user-1" });
                    }));

                    var client = app.GetTestClient();
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/CLI")
                    {
                        Content = new StringContent("{", Encoding.UTF8, "application/json")
                    };
                    request.Headers.TryAddWithoutValidation(DataHubEndpointDiagnostics.CorrelationIdHeaderName, "corr-cli-json");

                    var response = await client.SendAsync(request);

                    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                    var error = await ReadErrorResponseAsync(response);
                    error.Category.Should().Be(DataHubErrorCategory.DeserializationFailed);
                    error.CorrelationId.Should().Be("corr-cli-json");

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
    public async Task Cli_endpoint_should_attach_authenticated_user_before_runtime_dispatch()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Cli_endpoint_should_attach_authenticated_user_before_runtime_dispatch)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Cli_endpoint_should_attach_authenticated_user_before_runtime_dispatch))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var deserializedRequest = new EndpointCliRequest();
                    var user = new User { id = "user-1", TenantId = "tenant-1" };
                    RecordingMediator? capturedMediator = null;
                    await using var app = await CreateAppAsync(mediator =>
                    {
                        capturedMediator = mediator;
                        mediator.TrySendResponse = deserializedRequest;
                        mediator.SendResponse = new EndpointTestResponse { Success = true };
                    }, app => app.MapDataHubCliEndpoint("/api/CLI", options =>
                    {
                        options.AuthenticateAsync = (_, _) => Task.FromResult(user);
                    }));

                    var client = app.GetTestClient();
                    var response = await client.PostAsync("/api/CLI", JsonContent(new SerializedRequest
                    {
                        RequestType = nameof(EndpointCliRequest),
                        CorrelationId = "corr-cli-valid",
                        Data = "{}"
                    }));

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    capturedMediator.Should().NotBeNull();
                    capturedMediator!.TrySendRequests.Should().ContainSingle()
                        .Which.Should().BeOfType<DeserializeCliRequestRequest>()
                        .Which.SerializedRequest.CorrelationId.Should().Be("corr-cli-valid");
                    capturedMediator.RuntimeSendRequests.Should().ContainSingle().Which.Should().BeSameAs(deserializedRequest);
                    deserializedRequest.User.Should().BeSameAs(user);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static async Task<WebApplication> CreateAppAsync(
        Action<RecordingMediator> configureMediator,
        Action<WebApplication> mapEndpoints,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var mediator = new RecordingMediator();
        configureMediator(mediator);
        builder.Services.AddSingleton<IMediator>(mediator);
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        mapEndpoints(app);
        await app.StartAsync();
        return app;
    }

    private static StringContent JsonContent(object value)
    {
        return new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json");
    }

    private static async Task<DataHubErrorResponse> ReadErrorResponseAsync(HttpResponseMessage response)
    {
        return JsonConvert.DeserializeObject<DataHubErrorResponse>(await response.Content.ReadAsStringAsync())!;
    }

    private static Activity FindDataHubActivity(ActivityCapture capture, string correlationId)
    {
        return capture.StoppedActivities.Last(activity =>
            Equals(activity.GetTagItem("datahub.correlation_id"), correlationId));
    }

    private static void AddTestBearerAuthorizationServices(IServiceCollection services)
    {
        services.AddSingleton(Options.Create(new DataHubClientAzureAdOptions
        {
            TenantId = "tenant-1",
            Audience = "api://datahub-api",
            AllowedClientIds = new[] { "allowed-client-id" },
            AllowedObjectIds = new[] { "allowed-object-id" }
        }));
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, TestBearerAuthenticationHandler>(
                DataHubClientEndpointAuthenticationDefaults.AzureAdBearerScheme,
                _ => { });
    }

    private static void AddTestCliBearerAuthenticationServices(IServiceCollection services)
    {
        services.AddSingleton(Options.Create(new DataHubCliAzureAdOptions
        {
            TenantId = "tenant-1",
            Audience = "api://datahub-api",
            RequiredScope = "datahub_cli"
        }));
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, TestBearerAuthenticationHandler>(
                DataHubCliEndpointAuthenticationDefaults.AzureAdBearerScheme,
                _ => { });
    }

    private static void AddSignedJwtBearerAuthorizationServices(IServiceCollection services)
    {
        services.AddSingleton(Options.Create(new DataHubClientAzureAdOptions
        {
            TenantId = "tenant-1",
            Audience = "api://datahub-api",
            AllowedClientIds = new[] { "allowed-client-id" },
            AllowedObjectIds = new[] { "allowed-object-id" }
        }));
        services.AddAuthentication()
            .AddJwtBearer(DataHubClientEndpointAuthenticationDefaults.AzureAdBearerScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "https://login.microsoftonline.com/tenant-1/v2.0",
                    ValidateAudience = true,
                    ValidAudience = "api://datahub-api",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SignedJwtKey))
                };
            });
    }

    private static string CreateSignedJwt(string issuer, string audience, params Claim[] claims)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SignedJwtKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class EndpointTestResponse
    {
        public bool Success { get; set; }
    }

    private sealed class EndpointSecretResponse
    {
        public bool Success { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;
    }

    private sealed class EndpointClientRequest : IRequest<EndpointTestResponse>
    {
    }

    private sealed class EndpointCliRequest : DataHubCLIRequest<EndpointTestResponse>
    {
    }

    private sealed class RecordingMediator : IMediator
    {
        public List<IRequest> TrySendRequests { get; } = new();
        public List<IRequest> RuntimeSendRequests { get; } = new();
        public object? TrySendResponse { get; set; }
        public Func<IRequest, object?>? TrySendResponseFactory { get; set; }
        public object? SendResponse { get; set; }
        public Exception? TrySendException { get; set; }
        public Exception? SendException { get; set; }

        public Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            if (SendException != null)
            {
                return Task.FromResult<OneOf<TResponse, Exception>>(SendException);
            }

            return Task.FromResult<OneOf<TResponse, Exception>>((TResponse)SendResponse!);
        }

        public Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
        {
            RuntimeSendRequests.Add(request);
            if (SendException != null)
            {
                return Task.FromResult<OneOf<object, Exception>>(SendException);
            }

            return Task.FromResult<OneOf<object, Exception>>(SendResponse!);
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
            TrySendRequests.Add(request);
            if (TrySendException != null)
            {
                return Task.FromResult<(TResponse?, Exception?)>((default, TrySendException));
            }

            var response = TrySendResponseFactory?.Invoke(request) ?? TrySendResponse;
            return Task.FromResult(((TResponse?)response, (Exception?)null));
        }
    }

    private sealed class ResolvingAuthorizationService : IDataHubAuthorizationService
    {
        private readonly List<string> _effectivePermissions;

        public ResolvingAuthorizationService(IEnumerable<string> effectivePermissions)
        {
            _effectivePermissions = effectivePermissions.ToList();
        }

        public Task<User> ResolveEffectivePermissionsAsync(User user, CancellationToken cancellationToken)
        {
            user.EffectivePermissions = _effectivePermissions;
            return Task.FromResult(user);
        }

        public Task<bool> RoleExistsAsync(string tenantId, string roleName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ValidateRoleReferencesAsync(string tenantId, IEnumerable<string> roleNames, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestBearerAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorization = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authorization))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = authorization switch
            {
                "Bearer allowed-client" => new[] { new Claim("azp", "allowed-client-id") },
                "Bearer allowed-appid" => new[] { new Claim("appid", "allowed-client-id") },
                "Bearer allowed-object" => new[] { new Claim("oid", "allowed-object-id") },
                "Bearer delegated-client" => new[]
                {
                    new Claim("azp", "allowed-client-id"),
                    new Claim("scp", "DataHub.Write")
                },
                "Bearer cli-valid" => new[]
                {
                    new Claim("tid", "tenant-1"),
                    new Claim("oid", "object-1"),
                    new Claim("preferred_username", "admin@contoso.test"),
                    new Claim("scp", "datahub_cli other-scope")
                },
                "Bearer cli-app-only" => new[]
                {
                    new Claim("tid", "tenant-1"),
                    new Claim("oid", "object-1"),
                    new Claim("azp", "client-1")
                },
                "Bearer cli-wrong-scope" => new[]
                {
                    new Claim("tid", "tenant-1"),
                    new Claim("oid", "object-1"),
                    new Claim("preferred_username", "admin@contoso.test"),
                    new Claim("scp", "other-scope")
                },
                _ => new[] { new Claim("azp", "denied-client-id") }
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
