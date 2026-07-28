using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reimaginate.DataHub.AspNetCore.Readiness;
using Reimaginate.DataHub.Config;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DataHubReadinessEndpointTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task Readyz_should_return_healthy_when_dependency_checks_are_skipped_by_configuration()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Readyz_should_return_healthy_when_dependency_checks_are_skipped_by_configuration)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Readyz_should_return_healthy_when_dependency_checks_are_skipped_by_configuration))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(new Dictionary<string, string?>
                    {
                        ["DataHub:DataStoreOptions:UseDatabase"] = "InMemory",
                        ["DataHub:ProcessingLockOptions:UseRepository"] = "InMemory"
                    });

                    var response = await app.GetTestClient().GetAsync("/readyz");

                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                    body.Should().Contain("\"status\":\"Healthy\"");
                    body.Should().Contain("Cosmos check skipped");
                    body.Should().Contain("Processing lock check skipped");
                    body.Should().Contain("Event Grid check skipped");

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
    public async Task Readyz_should_return_unhealthy_when_event_grid_is_selected_without_required_config()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Readyz_should_return_unhealthy_when_event_grid_is_selected_without_required_config)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Readyz_should_return_unhealthy_when_event_grid_is_selected_without_required_config))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(
                        new Dictionary<string, string?>
                        {
                            ["DataHub:DataStoreOptions:UseDatabase"] = "InMemory",
                            ["DataHub:ProcessingLockOptions:UseRepository"] = "InMemory"
                        },
                        services =>
                        {
                            services.AddSingleton(Options.Create(new NotificationServiceOptions
                            {
                                UseMessagingService = "AzureEventGrid",
                                AzureEventGridClientOptions = new AzureEventGridClientOptions()
                            }));
                        });

                    var response = await app.GetTestClient().GetAsync("/readyz");

                    response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
                    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                    body.Should().Contain("\"status\":\"Unhealthy\"");
                    body.Should().Contain("EventGridUrl or EventGridKey is missing");

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
    public async Task Readyz_should_cache_results_within_cache_window()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Readyz_should_cache_results_within_cache_window)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Readyz_should_cache_results_within_cache_window))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    await using var app = await CreateAppAsync(
                        new Dictionary<string, string?>
                        {
                            ["DataHub:DataStoreOptions:UseDatabase"] = "InMemory",
                            ["DataHub:ProcessingLockOptions:UseRepository"] = "InMemory"
                        },
                        services =>
                        {
                            services.AddSingleton(Options.Create(new NotificationServiceOptions
                            {
                                UseMessagingService = "AzureEventGrid",
                                AzureEventGridClientOptions = new AzureEventGridClientOptions()
                            }));
                        },
                        options => options.CacheDuration = TimeSpan.FromMinutes(1));

                    var first = await app.GetTestClient().GetAsync("/readyz");
                    ReplaceNotificationOptions(app.Services, new NotificationServiceOptions
                    {
                        UseMessagingService = "AzureEventGrid",
                        AzureEventGridClientOptions = new AzureEventGridClientOptions
                        {
                            EventGridUrl = "https://event-grid.test",
                            EventGridKey = "key"
                        }
                    });

                    var second = await app.GetTestClient().GetAsync("/readyz");

                    first.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
                    second.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
                    var body = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                    body.Should().Contain("EventGridUrl or EventGridKey is missing");

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
        Dictionary<string, string?> configuration,
        Action<IServiceCollection>? configureServices = null,
        Action<DataHubReadinessOptions>? configureReadiness = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddSingleton(Options.Create(new NotificationServiceOptions()));
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.MapDataHubReadinessEndpoint("/readyz", configureReadiness);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static void ReplaceNotificationOptions(IServiceProvider services, NotificationServiceOptions options)
    {
        var current = services.GetRequiredService<IOptions<NotificationServiceOptions>>().Value;
        current.UseMessagingService = options.UseMessagingService;
        current.AzureEventGridClientOptions = options.AzureEventGridClientOptions;
    }
}
