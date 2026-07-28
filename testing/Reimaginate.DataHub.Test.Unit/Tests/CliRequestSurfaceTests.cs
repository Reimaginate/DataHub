using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Config;
using Reimaginate.DataHub.Requests.External.CLI.DeserializeCliRequest;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class CliRequestSurfaceTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task All_cli_requests_should_set_request_type_to_clr_type_name()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(All_cli_requests_should_set_request_type_to_clr_type_name)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(All_cli_requests_should_set_request_type_to_clr_type_name))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mismatches = GetCliRequestTypes()
                        .Select(type => new
                        {
                            Type = type,
                            Request = (DataHubCLIRequest)Activator.CreateInstance(type)!
                        })
                        .Where(item => item.Request.RequestType != item.Type.Name)
                        .Select(item => $"{item.Type.Name}: {item.Request.RequestType ?? "<null>"}")
                        .ToList();

                    mismatches.Should().BeEmpty("each CLI request envelope uses RequestType to resolve the concrete request class");

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
    public async Task All_cli_requests_should_deserialize_and_preserve_correlation_id()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(All_cli_requests_should_deserialize_and_preserve_correlation_id)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(All_cli_requests_should_deserialize_and_preserve_correlation_id))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeCliRequestRequestHandler();
                    var failures = new List<string>();

                    foreach (var requestType in GetCliRequestTypes())
                    {
                        try
                        {
                            var result = await handler.HandleAsync(new DeserializeCliRequestRequest
                            {
                                SerializedRequest = new SerializedRequest
                                {
                                    RequestType = requestType.Name,
                                    CorrelationId = $"corr-{requestType.Name}",
                                    Data = "{}"
                                }
                            }, CancellationToken.None);

                            result.GetType().Should().Be(requestType);
                            ((DataHubCLIRequest)result).CorrelationId.Should().Be($"corr-{requestType.Name}");
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
    public async Task Cli_query_requests_should_deserialize_parameters()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Cli_query_requests_should_deserialize_parameters)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Cli_query_requests_should_deserialize_parameters))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeCliRequestRequestHandler();

                    var result = await handler.HandleAsync(new DeserializeCliRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = nameof(GetEntitiesWhereRequest),
                            CorrelationId = "corr-cli-params",
                            Data = JsonConvert.SerializeObject(new
                            {
                                WhereClause = "x.id = @id and x.Enabled = @enabled",
                                Parameters = new object[]
                                {
                                    new { Name = "id", Value = "entity-1" },
                                    new { Name = "enabled", Value = true }
                                }
                            })
                        }
                    }, CancellationToken.None);

                    var request = result.Should().BeOfType<GetEntitiesWhereRequest>().Subject;
                    request.Parameters.Should().HaveCount(2);
                    request.Parameters.Should().Contain(parameter => parameter.Name == "id" && Equals(parameter.Value, "entity-1"));
                    request.Parameters.Should().Contain(parameter => parameter.Name == "enabled" && Equals(parameter.Value, true));

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
    public async Task All_cli_requests_should_have_registered_handlers()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(All_cli_requests_should_have_registered_handlers)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(All_cli_requests_should_have_registered_handlers))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var services = new ServiceCollection();
                    services.AddDataHub(options => options
                        .WithAppSettingsConfig(CreateConfig())
                        .WithDatabase(database => database.UseInMemoryDatabase())
                        .WithProcessingLockOptions(locks => locks.UseInMemoryRepository()));

                    var missingHandlers = GetCliRequestTypes()
                        .Select(requestType => new
                        {
                            RequestType = requestType,
                            ResponseType = GetResponseType(requestType)
                        })
                        .Where(item => !services.Any(descriptor =>
                            descriptor.ServiceType == typeof(IHandler<,>).MakeGenericType(item.RequestType, item.ResponseType)))
                        .Select(item => $"{item.RequestType.Name} -> {item.ResponseType.Name}")
                        .ToList();

                    missingHandlers.Should().BeEmpty("every public CLI request should be dispatchable through the generated mediator");

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
    public async Task All_cli_requests_should_have_validator_targeting_the_same_request_type()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(All_cli_requests_should_have_validator_targeting_the_same_request_type)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(All_cli_requests_should_have_validator_targeting_the_same_request_type))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var validatorTargets = typeof(DeserializeCliRequestRequestHandler).Assembly.GetTypes()
                        .Where(type => !type.IsAbstract && !type.IsInterface)
                        .Select(type => new
                        {
                            ValidatorType = type,
                            RequestType = GetAbstractValidatorRequestType(type)
                        })
                        .Where(item => item.RequestType != null)
                        .ToLookup(item => item.RequestType!, item => item.ValidatorType);

                    var missingValidators = GetCliRequestTypes()
                        .Where(requestType => !validatorTargets.Contains(requestType))
                        .Select(type => type.Name)
                        .ToList();

                    missingValidators.Should().BeEmpty("validators must target the concrete CLI request type they validate");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static IReadOnlyList<Type> GetCliRequestTypes()
    {
        return typeof(DataHubCLIRequest).Assembly.GetTypes()
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(DataHubCLIRequest).IsAssignableFrom(type))
            .OrderBy(type => type.Name)
            .ToList();
    }

    private static Type GetResponseType(Type requestType)
    {
        return requestType.GetInterfaces()
            .Single(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>))
            .GetGenericArguments()[0];
    }

    private static Type? GetAbstractValidatorRequestType(Type validatorType)
    {
        for (var type = validatorType; type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static IConfiguration CreateConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
    }
}
