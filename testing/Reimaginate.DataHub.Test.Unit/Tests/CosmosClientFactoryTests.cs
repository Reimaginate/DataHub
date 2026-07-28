using Azure.Identity;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Reimaginate.DataHub.Config;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class CosmosClientFactoryTests : ScenarioUnitTestBase
{
    private const string TestConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEe6kNUZpJjvI6gm1DPXVsA7EGHIj1R0CmR6I+O4QvjphgT2lW4yqnUyQ==;"; // gitleaks:allow -- Microsoft-documented Cosmos DB emulator credential.
    private const string TestAccountEndpoint = "https://datahub-test.documents.azure.com:443/";
    private const string TestTenantId = "11111111-1111-1111-1111-111111111111";
    private const string TestClientId = "22222222-2222-2222-2222-222222222222";
    private const string TestClientSecret = "test-client-secret";
    private const string TestManagedIdentityClientId = "33333333-3333-3333-3333-333333333333";

    [Fact]
    public async Task Create_should_default_to_connection_string_mode()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Create_should_default_to_connection_string_mode)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Create_should_default_to_connection_string_mode))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var options = new CosmosDbOptions
                    {
                        ConnString = TestConnectionString
                    };

                    using var client = CosmosClientFactory.Create(options, CreateClientOptions());

                    options.AuthenticationMode.Should().Be(CosmosDbAuthenticationMode.ConnectionString);
                    client.Should().NotBeNull();

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
    public async Task Create_should_support_application_registration_mode()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Create_should_support_application_registration_mode)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Create_should_support_application_registration_mode))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var options = new CosmosDbOptions
                    {
                        AuthenticationMode = CosmosDbAuthenticationMode.ApplicationRegistration,
                        AccountEndpoint = TestAccountEndpoint,
                        TenantId = TestTenantId,
                        ClientId = TestClientId,
                        ClientSecret = TestClientSecret
                    };

                    using var client = CosmosClientFactory.Create(options, CreateClientOptions());

                    client.Should().NotBeNull();
                    CosmosClientFactory.CreateCredential(options).Should().BeOfType<ClientSecretCredential>();

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
    public async Task Create_should_support_system_assigned_managed_identity_mode()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Create_should_support_system_assigned_managed_identity_mode)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Create_should_support_system_assigned_managed_identity_mode))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var options = new CosmosDbOptions
                    {
                        AuthenticationMode = CosmosDbAuthenticationMode.ManagedIdentity,
                        AccountEndpoint = TestAccountEndpoint
                    };

                    using var client = CosmosClientFactory.Create(options, CreateClientOptions());

                    client.Should().NotBeNull();
                    CosmosClientFactory.CreateCredential(options).Should().BeOfType<ManagedIdentityCredential>();

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
    public async Task Create_should_support_user_assigned_managed_identity_mode()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Create_should_support_user_assigned_managed_identity_mode)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Create_should_support_user_assigned_managed_identity_mode))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var options = new CosmosDbOptions
                    {
                        AuthenticationMode = CosmosDbAuthenticationMode.ManagedIdentity,
                        AccountEndpoint = TestAccountEndpoint,
                        ManagedIdentityClientId = TestManagedIdentityClientId
                    };

                    using var client = CosmosClientFactory.Create(options, CreateClientOptions());

                    client.Should().NotBeNull();
                    CosmosClientFactory.CreateCredential(options).Should().BeOfType<ManagedIdentityCredential>();

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
    [InlineData(CosmosDbAuthenticationMode.ConnectionString, null, null, null, null, "ConnString")]
    [InlineData(CosmosDbAuthenticationMode.ApplicationRegistration, null, TestTenantId, TestClientId, TestClientSecret, "AccountEndpoint")]
    [InlineData(CosmosDbAuthenticationMode.ApplicationRegistration, TestAccountEndpoint, null, TestClientId, TestClientSecret, "TenantId")]
    [InlineData(CosmosDbAuthenticationMode.ApplicationRegistration, TestAccountEndpoint, TestTenantId, null, TestClientSecret, "ClientId")]
    [InlineData(CosmosDbAuthenticationMode.ApplicationRegistration, TestAccountEndpoint, TestTenantId, TestClientId, null, "ClientSecret")]
    [InlineData(CosmosDbAuthenticationMode.ManagedIdentity, null, null, null, null, "AccountEndpoint")]
    public async Task Create_should_throw_clear_error_when_selected_mode_is_missing_required_option(
        CosmosDbAuthenticationMode authenticationMode,
        string? accountEndpoint,
        string? tenantId,
        string? clientId,
        string? clientSecret,
        string expectedOptionName)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Create_should_throw_clear_error_when_selected_mode_is_missing_required_option)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Create_should_throw_clear_error_when_selected_mode_is_missing_required_option))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var options = new CosmosDbOptions
                    {
                        AuthenticationMode = authenticationMode,
                        ConnString = authenticationMode == CosmosDbAuthenticationMode.ConnectionString ? null : TestConnectionString,
                        AccountEndpoint = accountEndpoint,
                        TenantId = tenantId,
                        ClientId = clientId,
                        ClientSecret = clientSecret
                    };

                    Action act = () => CosmosClientFactory.Create(options, CreateClientOptions());

                    act.Should()
                        .Throw<InvalidOperationException>()
                        .WithMessage($"*CosmosDbOptions.{expectedOptionName}*AuthenticationMode is {authenticationMode}*");

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
    public async Task Configuration_should_bind_identity_authentication_options()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Configuration_should_bind_identity_authentication_options)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Configuration_should_bind_identity_authentication_options))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var config = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["AuthenticationMode"] = "ApplicationRegistration",
                            ["AccountEndpoint"] = TestAccountEndpoint,
                            ["TenantId"] = TestTenantId,
                            ["ClientId"] = TestClientId,
                            ["ClientSecret"] = TestClientSecret,
                            ["ManagedIdentityClientId"] = TestManagedIdentityClientId
                        })
                        .Build();

                    var options = new CosmosDbOptions();
                    config.Bind(options);

                    options.AuthenticationMode.Should().Be(CosmosDbAuthenticationMode.ApplicationRegistration);
                    options.AccountEndpoint.Should().Be(TestAccountEndpoint);
                    options.TenantId.Should().Be(TestTenantId);
                    options.ClientId.Should().Be(TestClientId);
                    options.ClientSecret.Should().Be(TestClientSecret);
                    options.ManagedIdentityClientId.Should().Be(TestManagedIdentityClientId);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static CosmosClientOptions CreateClientOptions()
    {
        return new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway
        };
    }
}
