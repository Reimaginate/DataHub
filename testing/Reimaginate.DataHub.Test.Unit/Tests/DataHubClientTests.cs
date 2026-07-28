using System.Net;
using System.Reflection;
using Azure.Core;
using Azure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Reimaginate.DataHub.Client;
using Reimaginate.DataHub.Client.Config;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DataHubClientTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task PostRequestAsync_should_send_shared_key_header_by_default()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(PostRequestAsync_should_send_shared_key_header_by_default)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(PostRequestAsync_should_send_shared_key_header_by_default))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}")
                    });
                    var client = new DataHubClient(new HttpClient(handler), new DataHubClientOptions
                    {
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        Key = "test-key"
                    });

                    await client.PostRequestAsync<GetDataHubEntityRequest, GetDataHubEntityResponse>(
                        new GetDataHubEntityRequest(),
                        CancellationToken.None);

                    handler.Request.Headers.GetValues("x-functions-key").Should().ContainSingle("test-key");
                    handler.Request.Headers.Authorization.Should().BeNull();

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
    public async Task PostRequestAsync_should_serialize_trace_options_into_request_envelope()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(PostRequestAsync_should_serialize_trace_options_into_request_envelope)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(PostRequestAsync_should_serialize_trace_options_into_request_envelope))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}")
                    });
                    var client = new DataHubClient(new HttpClient(handler), new DataHubClientOptions
                    {
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        Key = "test-key"
                    });
                    var expiresOn = DateTimeOffset.Parse("2026-06-21T03:00:00Z");

                    await client.PostRequestAsync<GetDataHubEntityRequest, GetDataHubEntityResponse>(
                        new GetDataHubEntityRequest
                        {
                            CorrelationId = "corr-trace",
                            TraceOptions = new DataHubTraceOptions
                            {
                                Enabled = true,
                                IncludeRequest = true,
                                IncludeResponse = true,
                                Reason = "investigate merge failure",
                                ExpiresOn = expiresOn
                            }
                        },
                        CancellationToken.None);

                    var envelope = JsonConvert.DeserializeObject<SerializedRequest>(handler.RequestBody)!;
                    envelope.TraceOptions.Should().NotBeNull();
                    envelope.TraceOptions.Enabled.Should().BeTrue();
                    envelope.TraceOptions.IncludeRequest.Should().BeTrue();
                    envelope.TraceOptions.IncludeResponse.Should().BeTrue();
                    envelope.TraceOptions.Reason.Should().Be("investigate merge failure");
                    envelope.TraceOptions.ExpiresOn.Should().Be(expiresOn);

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
    public async Task PostRequestAsync_should_send_bearer_token_for_application_registration_authentication()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(PostRequestAsync_should_send_bearer_token_for_application_registration_authentication)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(PostRequestAsync_should_send_bearer_token_for_application_registration_authentication))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var credential = new TestTokenCredential("access-token");
                    var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}")
                    });
                    var client = new DataHubClient(new HttpClient(handler), new DataHubClientOptions
                    {
                        AuthenticationMode = DataHubClientAuthenticationMode.ApplicationRegistration,
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        AzureAdScope = "api://datahub-api/.default",
                        TenantId = "tenant-1",
                        ClientId = "client-1",
                        ClientSecret = "secret-1"
                    }, credential);

                    await client.PostRequestAsync<GetDataHubEntityRequest, GetDataHubEntityResponse>(
                        new GetDataHubEntityRequest(),
                        CancellationToken.None);

                    handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
                    handler.Request.Headers.Authorization.Parameter.Should().Be("access-token");
                    credential.Scopes.Should().ContainSingle("api://datahub-api/.default");
                    handler.Request.Headers.Contains("x-functions-key").Should().BeFalse();

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
    public async Task PostRequestAsync_should_send_bearer_token_for_managed_identity_authentication()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(PostRequestAsync_should_send_bearer_token_for_managed_identity_authentication)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(PostRequestAsync_should_send_bearer_token_for_managed_identity_authentication))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var credential = new TestTokenCredential("managed-identity-token");
                    var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}")
                    });
                    var client = new DataHubClient(new HttpClient(handler), new DataHubClientOptions
                    {
                        AuthenticationMode = DataHubClientAuthenticationMode.ManagedIdentity,
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        AzureAdScope = "api://datahub-api/.default",
                        ManagedIdentityClientId = "33333333-3333-3333-3333-333333333333"
                    }, credential);

                    await client.PostRequestAsync<GetDataHubEntityRequest, GetDataHubEntityResponse>(
                        new GetDataHubEntityRequest(),
                        CancellationToken.None);

                    handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
                    handler.Request.Headers.Authorization.Parameter.Should().Be("managed-identity-token");
                    credential.Scopes.Should().ContainSingle("api://datahub-api/.default");
                    handler.Request.Headers.Contains("x-functions-key").Should().BeFalse();

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
    public async Task CreateCredential_should_create_application_registration_credential()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateCredential_should_create_application_registration_credential)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateCredential_should_create_application_registration_credential))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var credential = DataHubClientCredentialFactory.CreateCredential(new DataHubClientOptions
                    {
                        AuthenticationMode = DataHubClientAuthenticationMode.ApplicationRegistration,
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        AzureAdScope = "api://datahub-api/.default",
                        TenantId = "11111111-1111-1111-1111-111111111111",
                        ClientId = "22222222-2222-2222-2222-222222222222",
                        ClientSecret = "secret"
                    });

                    credential.Should().BeOfType<ClientSecretCredential>();

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
    [InlineData("33333333-3333-3333-3333-333333333333")]
    public async Task CreateCredential_should_create_managed_identity_credential(string? managedIdentityClientId)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateCredential_should_create_managed_identity_credential)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateCredential_should_create_managed_identity_credential))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var credential = DataHubClientCredentialFactory.CreateCredential(new DataHubClientOptions
                    {
                        AuthenticationMode = DataHubClientAuthenticationMode.ManagedIdentity,
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        AzureAdScope = "api://datahub-api/.default",
                        ManagedIdentityClientId = managedIdentityClientId
                    });

                    credential.Should().BeOfType<ManagedIdentityCredential>();

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
    public async Task CreateCredential_should_reject_shared_key_mode()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateCredential_should_reject_shared_key_mode)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateCredential_should_reject_shared_key_mode))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var options = new DataHubClientOptions
                    {
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        Key = "test-key"
                    };

                    var act = () => DataHubClientCredentialFactory.CreateCredential(options);

                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage("*SharedKey*does not use Azure identity credentials*");

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
    public async Task Constructor_should_require_token_credential_for_identity_modes_when_injected()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Constructor_should_require_token_credential_for_identity_modes_when_injected)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Constructor_should_require_token_credential_for_identity_modes_when_injected))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var act = () => new DataHubClient(
                        new HttpClient(new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
                        new DataHubClientOptions
                        {
                            AuthenticationMode = DataHubClientAuthenticationMode.ManagedIdentity,
                            DataHubClientUrl = "https://datahub.test/api/Client",
                            AzureAdScope = "api://datahub-api/.default"
                        },
                        null!);

                    act.Should().Throw<ArgumentNullException>()
                        .WithParameterName("credential");

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
    public async Task WithAppSettingsConfig_should_bind_identity_authentication_options()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(WithAppSettingsConfig_should_bind_identity_authentication_options)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(WithAppSettingsConfig_should_bind_identity_authentication_options))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var config = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["DataHubClient:AuthenticationMode"] = "ApplicationRegistration",
                            ["DataHubClient:DataHubClientUrl"] = "https://datahub.test/api/Client",
                            ["DataHubClient:Key"] = "ignored-key",
                            ["DataHubClient:AzureAdScope"] = "api://datahub-api/.default",
                            ["DataHubClient:TenantId"] = "tenant-1",
                            ["DataHubClient:ClientId"] = "client-1",
                            ["DataHubClient:ClientSecret"] = "secret-1",
                            ["DataHubClient:ManagedIdentityClientId"] = "managed-identity-1"
                        })
                        .Build();
                    var addOptions = new AddDataHubClientOptions();

                    addOptions.WithAppSettingsConfig(config, "DataHubClient");

                    var boundOptions = (DataHubClientOptions)typeof(AddDataHubClientOptions)
                        .GetProperty("DataHubClientOptions", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(addOptions)!;

                    boundOptions.AuthenticationMode.Should().Be(DataHubClientAuthenticationMode.ApplicationRegistration);
                    boundOptions.DataHubClientUrl.Should().Be("https://datahub.test/api/Client");
                    boundOptions.Key.Should().Be("ignored-key");
                    boundOptions.AzureAdScope.Should().Be("api://datahub-api/.default");
                    boundOptions.TenantId.Should().Be("tenant-1");
                    boundOptions.ClientId.Should().Be("client-1");
                    boundOptions.ClientSecret.Should().Be("secret-1");
                    boundOptions.ManagedIdentityClientId.Should().Be("managed-identity-1");

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
    [InlineData(DataHubClientAuthenticationMode.SharedKey, null, null, null, null, null, "Key")]
    [InlineData(DataHubClientAuthenticationMode.ApplicationRegistration, "test-key", null, "tenant-1", "client-1", "secret-1", "AzureAdScope")]
    [InlineData(DataHubClientAuthenticationMode.ApplicationRegistration, "test-key", "api://datahub-api/.default", null, "client-1", "secret-1", "TenantId")]
    [InlineData(DataHubClientAuthenticationMode.ApplicationRegistration, "test-key", "api://datahub-api/.default", "tenant-1", null, "secret-1", "ClientId")]
    [InlineData(DataHubClientAuthenticationMode.ApplicationRegistration, "test-key", "api://datahub-api/.default", "tenant-1", "client-1", null, "ClientSecret")]
    [InlineData(DataHubClientAuthenticationMode.ManagedIdentity, "test-key", null, null, null, null, "AzureAdScope")]
    public async Task CreateCredential_should_reject_missing_selected_authentication_options(
        DataHubClientAuthenticationMode authenticationMode,
        string? key,
        string? azureAdScope,
        string? tenantId,
        string? clientId,
        string? clientSecret,
        string expectedOptionName)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateCredential_should_reject_missing_selected_authentication_options)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateCredential_should_reject_missing_selected_authentication_options))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var options = new DataHubClientOptions
                    {
                        AuthenticationMode = authenticationMode,
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        Key = key,
                        AzureAdScope = azureAdScope,
                        TenantId = tenantId,
                        ClientId = clientId,
                        ClientSecret = clientSecret
                    };

                    var act = () => DataHubClientCredentialFactory.CreateCredential(options);

                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage($"*DataHubClientOptions.{expectedOptionName}*AuthenticationMode is {authenticationMode}*");

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
    public async Task PostRequestAsync_should_throw_diagnostic_exception_for_non_success_response()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(PostRequestAsync_should_throw_diagnostic_exception_for_non_success_response)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(PostRequestAsync_should_throw_diagnostic_exception_for_non_success_response))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var errorResponse = new DataHubErrorResponse
                    {
                        ErrorId = "error-1",
                        CorrelationId = "corr-1",
                        RequestType = nameof(GetDataHubEntityRequest),
                        Category = DataHubErrorCategory.InvalidRequest,
                        Message = "Request failed validation.",
                        Details =
                        [
                            new DataHubErrorDetail
                            {
                                Field = "EntityId",
                                Code = "NotEmptyValidator",
                                Message = "EntityId is required."
                            }
                        ]
                    };
                    var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        ReasonPhrase = "Bad Request",
                        Content = new StringContent(JsonConvert.SerializeObject(errorResponse))
                    });
                    var client = new DataHubClient(new HttpClient(handler), new DataHubClientOptions
                    {
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        Key = "test-key"
                    });
                    var request = new GetDataHubEntityRequest
                    {
                        CorrelationId = "corr-1",
                        EntityType = "Contact",
                        EntityId = "contact-1"
                    };

                    var act = () => client.PostRequestAsync<GetDataHubEntityRequest, GetDataHubEntityResponse>(request, CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubClientException>();
                    exception.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                    exception.Which.RequestType.Should().Be(nameof(GetDataHubEntityRequest));
                    exception.Which.CorrelationId.Should().Be("corr-1");
                    exception.Which.ResponseBody.Should().Contain("Request failed validation.");
                    exception.Which.ErrorResponse.Should().NotBeNull();
                    exception.Which.ErrorResponse.Category.Should().Be(DataHubErrorCategory.InvalidRequest);
                    exception.Which.Message.Should().Contain("Details: EntityId (NotEmptyValidator): EntityId is required.");

                    handler.Request.Headers.GetValues("x-correlation-id").Should().ContainSingle("corr-1");
                    var envelope = JsonConvert.DeserializeObject<SerializedRequest>(handler.RequestBody);
                    envelope!.CorrelationId.Should().Be("corr-1");
                    envelope.RequestType.Should().Be(nameof(GetDataHubEntityRequest));

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
    public async Task PostRequestAsync_should_preserve_raw_body_when_error_response_is_not_json()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(PostRequestAsync_should_preserve_raw_body_when_error_response_is_not_json)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(PostRequestAsync_should_preserve_raw_body_when_error_response_is_not_json))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        ReasonPhrase = "Internal Server Error",
                        Content = new StringContent("plain text failure")
                    });
                    var client = new DataHubClient(new HttpClient(handler), new DataHubClientOptions
                    {
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        Key = "test-key"
                    });

                    var act = () => client.PostRequestAsync<GetDataHubEntityRequest, GetDataHubEntityResponse>(
                        new GetDataHubEntityRequest(),
                        CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubClientException>();
                    exception.Which.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                    exception.Which.ResponseBody.Should().Be("plain text failure");
                    exception.Which.ErrorResponse.Should().BeNull();
                    exception.Which.CorrelationId.Should().NotBeNullOrWhiteSpace();

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
    public async Task PostRequestAsync_should_generate_correlation_id_and_reuse_it_for_error_context()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(PostRequestAsync_should_generate_correlation_id_and_reuse_it_for_error_context)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(PostRequestAsync_should_generate_correlation_id_and_reuse_it_for_error_context))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        ReasonPhrase = "Bad Request",
                        Content = new StringContent("")
                    });
                    var client = new DataHubClient(new HttpClient(handler), new DataHubClientOptions
                    {
                        DataHubClientUrl = "https://datahub.test/api/Client",
                        Key = "test-key"
                    });
                    var request = new GetDataHubEntityRequest
                    {
                        EntityType = "Contact",
                        EntityId = "contact-1"
                    };

                    var act = () => client.PostRequestAsync<GetDataHubEntityRequest, GetDataHubEntityResponse>(request, CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubClientException>();
                    request.CorrelationId.Should().NotBeNullOrWhiteSpace();
                    exception.Which.CorrelationId.Should().Be(request.CorrelationId);
                    handler.Request.Headers.GetValues("x-correlation-id").Should().ContainSingle(request.CorrelationId);
                    var envelope = JsonConvert.DeserializeObject<SerializedRequest>(handler.RequestBody);
                    envelope!.CorrelationId.Should().Be(request.CorrelationId);
                    envelope.RequestType.Should().Be(nameof(GetDataHubEntityRequest));

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public HttpRequestMessage Request { get; private set; } = null!;
        public string RequestBody { get; private set; } = null!;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class TestTokenCredential : TokenCredential
    {
        private readonly string _token;

        public TestTokenCredential(string token)
        {
            _token = token;
        }

        public string[] Scopes { get; private set; } = Array.Empty<string>();

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Scopes = requestContext.Scopes;
            return new AccessToken(_token, DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Scopes = requestContext.Scopes;
            return new ValueTask<AccessToken>(new AccessToken(_token, DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}
