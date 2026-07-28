using System.CommandLine;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Config;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class EntitiesUpdateCommandTests
{
    [Theory]
    [InlineData("Status", "Open")]
    [InlineData("IsActive", "true")]
    [InlineData("Rank", "123")]
    [InlineData("RemovedAt", "null")]
    [InlineData("Tags", "[\"a\",\"b\"]")]
    [InlineData("Metadata", "{\"source\":\"cli\"}")]
    public async Task Entities_update_query_mode_sends_patch_entities_request_with_parsed_value(string property, string value)
    {
        var api = new CapturingCliApi
        {
            EntityResponses =
            [
                CreateEntity("venue-1", "Venue"),
                CreateEntity("venue-2", "Venue")
            ]
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "update",
            property,
            value,
            "--where",
            "x.entityType='Venue'",
            "--yes",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Messages.Select(message => message.RequestType).Should().Equal(nameof(GetEntitiesWhereRequest), nameof(PatchEntitiesRequest));

        var query = JObject.Parse(api.Messages[0].Data);
        query[nameof(GetEntitiesWhereRequest.WhereClause)]!.Value<string>().Should().Be("x.entityType='Venue'");

        var patch = GetPatchRequest(api);
        AssertPatchSideEffects(patch, expectedSilent: false, expectedDispatchNotifications: true);

        var requests = patch[nameof(PatchEntitiesRequest.Requests)]!.Values<JObject>().ToList();
        requests.Should().HaveCount(2);
        requests.Select(request => request![nameof(PatchEntityRequest.DataSource)]!.Value<string>()).Should().OnlyContain(dataSource => dataSource == DataSources.DataHub);
        requests.Select(request => request![nameof(PatchEntityRequest.EntityType)]!.Value<string>()).Should().OnlyContain(entityType => entityType == "Venue");
        requests.Select(request => request![nameof(PatchEntityRequest.EntityId)]!.Value<string>()).Should().BeEquivalentTo(["venue-1", "venue-2"]);

        var operation = requests[0]![nameof(PatchEntityRequest.Operations)]!.First!;
        operation[nameof(Patch.Operation)]!.Value<string>().Should().Be("set");
        operation[nameof(Patch.Path)]!.Value<string>().Should().Be(property);
        operation[nameof(Patch.Value)]!.Should().BeEquivalentTo(JToken.Parse(ToExpectedJson(value)));
    }

    [Theory]
    [InlineData(true, true, "--no-update-timestamp")]
    [InlineData(false, false, "--no-notifications")]
    [InlineData(true, false, "--no-update-timestamp", "--no-notifications")]
    [InlineData(true, true, "--silent")]
    [InlineData(false, false, "--silent-notifications")]
    [InlineData(false, false, "--notify-agents", "--no-notifications")]
    public async Task Entities_update_side_effect_options_control_patch_request(bool expectedSilent, bool expectedDispatchNotifications, params string[] sideEffectArgs)
    {
        var api = new CapturingCliApi { EntityResponses = [CreateEntity("venue-1", "Venue")] };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "update",
            "Status",
            "Open",
            "--where",
            "x.entityType='Venue'",
            "--yes",
            "--output",
            "json",
            .. sideEffectArgs
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        AssertPatchSideEffects(GetPatchRequest(api), expectedSilent, expectedDispatchNotifications);
    }

    [Fact]
    public async Task Entities_update_query_mode_treats_non_json_value_as_string()
    {
        var api = new CapturingCliApi { EntityResponses = [CreateEntity("venue-1", "Venue")] };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "update",
            "Status",
            "Open",
            "--where",
            "x.entityType='Venue'",
            "--yes",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        var patch = JObject.Parse(api.Messages[1].Data);
        var value = patch[nameof(PatchEntitiesRequest.Requests)]!.First![nameof(PatchEntityRequest.Operations)]!.First![nameof(Patch.Value)]!;
        value.Type.Should().Be(JTokenType.String);
        value.Value<string>().Should().Be("Open");
    }

    [Fact]
    public async Task Entities_update_id_mode_sends_get_by_id_then_patch_entities_request()
    {
        var api = new CapturingCliApi
        {
            EntityResponses =
            [
                CreateEntity("venue-1", "Venue"),
                CreateEntity("venue-2", "Venue")
            ]
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "update",
            "Status",
            "Closed",
            "--entity-type",
            "Venue",
            "--ids",
            "venue-1",
            "venue-2",
            "--yes",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Messages.Select(message => message.RequestType).Should().Equal(nameof(GetEntitiesByIdRequest), nameof(PatchEntitiesRequest));

        var getById = JObject.Parse(api.Messages[0].Data);
        getById[nameof(GetEntitiesByIdRequest.EntityType)]!.Value<string>().Should().Be("Venue");
        getById[nameof(GetEntitiesByIdRequest.EntityIds)]!.Values<string>().Should().BeEquivalentTo(["venue-1", "venue-2"]);

        var patch = JObject.Parse(api.Messages[1].Data);
        patch[nameof(PatchEntitiesRequest.Requests)]!.Values<JObject>().Select(request => request![nameof(PatchEntityRequest.EntityId)]!.Value<string>())
            .Should().BeEquivalentTo(["venue-1", "venue-2"]);
    }

    [Fact]
    public async Task Entities_update_applies_target_options_while_sending_requests()
    {
        var api = new CapturingCliApi { EntityResponses = [CreateEntity("venue-1", "Venue")] };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "update",
            "Status",
            "Open",
            "--where",
            "x.entityType='Venue'",
            "--profile",
            "dev",
            "--url",
            "https://datahub.test/api/cli",
            "--tenant-id",
            "tenant-1",
            "--scope",
            "api://scope/datahub_cli",
            "--yes",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Targets.Should().NotBeEmpty();
        api.Targets.Should().OnlyContain(target =>
            target != null &&
            target.Context == "dev" &&
            target.Url == "https://datahub.test/api/cli" &&
            target.TenantId == "tenant-1" &&
            target.Scope == "api://scope/datahub_cli");
    }

    [Theory]
    [InlineData("Status", "Open")]
    [InlineData("Status", "Open", "--where", "x.entityType='Venue'", "--entity-type", "Venue", "--ids", "venue-1")]
    [InlineData("Status", "Open", "--ids", "venue-1")]
    public async Task Entities_update_rejects_invalid_target_modes(params string[] args)
    {
        var api = new CapturingCliApi();
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var exitCode = await rootCommand.Parse(["entities", "update", .. args]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(2);
        api.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Entities_update_returns_failure_when_patch_response_contains_failures()
    {
        var api = new CapturingCliApi
        {
            EntityResponses = [CreateEntity("venue-1", "Venue")],
            PatchResponses =
            [
                new PatchEntityResponse
                {
                    Success = false,
                    DataSource = DataSources.DataHub,
                    EntityType = "Venue",
                    EntityId = "venue-1",
                    FailureReason = "ONE_OR_MORE_PATCH_OPERATIONS_FAILED"
                }
            ]
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "update",
            "Status",
            "Open",
            "--where",
            "x.entityType='Venue'",
            "--yes",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task Entities_update_reports_successful_noop_patch_as_matched_not_updated()
    {
        var api = new CapturingCliApi
        {
            EntityResponses = [CreateEntity("venue-1", "Venue", ("Status", "Open"))],
            PatchResponses =
            [
                new PatchEntityResponse
                {
                    Success = true,
                    Changed = false,
                    DataSource = DataSources.DataHub,
                    EntityType = "Venue",
                    EntityId = "venue-1"
                }
            ]
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var (exitCode, output) = await InvokeWithOutput(rootCommand, [
            "entities",
            "update",
            "Status",
            "Open",
            "--entity-type",
            "Venue",
            "--ids",
            "venue-1",
            "--yes",
            "--output",
            "json"
        ]);

        exitCode.Should().Be(0);
        var rows = JArray.Parse(output);
        rows[0][nameof(ResultRow.Matched)]!.Value<int>().Should().Be(1);
        rows[0][nameof(ResultRow.Updated)]!.Value<int>().Should().Be(0);
        rows[0][nameof(ResultRow.Failed)]!.Value<int>().Should().Be(0);
    }

    [Fact]
    public async Task Entities_update_counts_legacy_success_response_as_updated()
    {
        var api = new CapturingCliApi
        {
            EntityResponses = [CreateEntity("venue-1", "Venue")],
            PatchResponses =
            [
                new PatchEntityResponse
                {
                    Success = true,
                    DataSource = DataSources.DataHub,
                    EntityType = "Venue",
                    EntityId = "venue-1"
                }
            ]
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var (exitCode, output) = await InvokeWithOutput(rootCommand, [
            "entities",
            "update",
            "Status",
            "Closed",
            "--entity-type",
            "Venue",
            "--ids",
            "venue-1",
            "--yes",
            "--output",
            "json"
        ]);

        exitCode.Should().Be(0);
        var rows = JArray.Parse(output);
        rows[0][nameof(ResultRow.Updated)]!.Value<int>().Should().Be(1);
    }

    [Fact]
    public async Task Entities_update_dry_run_query_mode_sends_only_get_request_and_outputs_preview()
    {
        var api = new CapturingCliApi
        {
            EntityResponses =
            [
                CreateEntity("venue-1", "Venue", ("Status", "Open")),
                CreateEntity("venue-2", "Venue", ("Status", "Draft"))
            ]
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var (exitCode, output) = await InvokeWithOutput(rootCommand, [
            "entities",
            "update",
            "Status",
            "Closed",
            "--where",
            "x.entityType='Venue'",
            "--dry-run",
            "--output",
            "json"
        ]);

        exitCode.Should().Be(0);
        api.Messages.Select(message => message.RequestType).Should().Equal(nameof(GetEntitiesWhereRequest));

        var rows = JArray.Parse(output);
        rows.Should().HaveCount(2);
        rows[0][nameof(PreviewRow.Matched)]!.Value<int>().Should().Be(2);
        rows[0][nameof(PreviewRow.EntityType)]!.Value<string>().Should().Be("Venue");
        rows[0][nameof(PreviewRow.EntityId)]!.Value<string>().Should().Be("venue-1");
        rows[0][nameof(PreviewRow.Property)]!.Value<string>().Should().Be("Status");
        rows[0][nameof(PreviewRow.CurrentValue)]!.Value<string>().Should().Be("Open");
        rows[0][nameof(PreviewRow.NewValue)]!.Value<string>().Should().Be("Closed");
    }

    [Fact]
    public async Task Entities_update_dry_run_id_mode_sends_only_get_by_id_request()
    {
        var api = new CapturingCliApi
        {
            EntityResponses =
            [
                CreateEntity("venue-1", "Venue", ("Rank", 1)),
                CreateEntity("venue-2", "Venue", ("Rank", 2))
            ]
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var (exitCode, output) = await InvokeWithOutput(rootCommand, [
            "entities",
            "update",
            "Rank",
            "3",
            "--entity-type",
            "Venue",
            "--ids",
            "venue-1",
            "venue-2",
            "--dry-run",
            "--output",
            "json"
        ]);

        exitCode.Should().Be(0);
        api.Messages.Select(message => message.RequestType).Should().Equal(nameof(GetEntitiesByIdRequest));

        var rows = JArray.Parse(output);
        rows.Should().HaveCount(2);
        rows[0][nameof(PreviewRow.CurrentValue)]!.Value<int>().Should().Be(1);
        rows[0][nameof(PreviewRow.NewValue)]!.Value<int>().Should().Be(3);
    }

    [Fact]
    public async Task Entities_update_dry_run_preserves_json_value_parsing()
    {
        var api = new CapturingCliApi
        {
            EntityResponses =
            [
                CreateEntity("venue-1", "Venue")
            ]
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var (exitCode, output) = await InvokeWithOutput(rootCommand, [
            "entities",
            "update",
            "Tags",
            "[\"a\",\"b\"]",
            "--where",
            "x.entityType='Venue'",
            "--dry-run",
            "--output",
            "json"
        ]);

        exitCode.Should().Be(0);
        var rows = JArray.Parse(output);
        rows[0][nameof(PreviewRow.CurrentValue)]!.Type.Should().Be(JTokenType.Null);
        rows[0][nameof(PreviewRow.NewValue)]!.Values<string>().Should().Equal("a", "b");
    }

    [Fact]
    public async Task Entities_update_query_mode_fetches_all_pages_before_patching()
    {
        var api = new CapturingCliApi
        {
            EntityResponsePages =
            [
                [CreateEntity("venue-1", "Venue")],
                [CreateEntity("venue-2", "Venue")]
            ]
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "update",
            "Status",
            "Closed",
            "--where",
            "x.entityType='Venue'",
            "--yes",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Messages.Select(message => message.RequestType).Should().Equal(
            nameof(GetEntitiesWhereRequest),
            nameof(GetEntitiesWhereRequest),
            nameof(PatchEntitiesRequest));

        var firstQuery = JObject.Parse(api.Messages[0].Data);
        firstQuery[nameof(GetEntitiesWhereRequest.GetTotalResultCount)]!.Value<bool>().Should().BeTrue();
        firstQuery[nameof(GetEntitiesWhereRequest.ContinuationToken)]?.Value<string>().Should().BeNullOrEmpty();

        var secondQuery = JObject.Parse(api.Messages[1].Data);
        secondQuery[nameof(GetEntitiesWhereRequest.GetTotalResultCount)]!.Value<bool>().Should().BeFalse();
        secondQuery[nameof(GetEntitiesWhereRequest.ContinuationToken)]!.Value<string>().Should().Be("page-2");

        var patch = JObject.Parse(api.Messages[2].Data);
        patch[nameof(PatchEntitiesRequest.Requests)]!.Values<JObject>()
            .Select(request => request![nameof(PatchEntityRequest.EntityId)]!.Value<string>())
            .Should().BeEquivalentTo(["venue-1", "venue-2"]);
    }

    [Fact]
    public async Task Entities_update_patches_targets_in_batches()
    {
        var api = new CapturingCliApi
        {
            EntityResponses = Enumerable.Range(1, 1001)
                .Select(index => CreateEntity($"venue-{index}", "Venue"))
                .ToList()
        };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "update",
            "Status",
            "Closed",
            "--where",
            "x.entityType='Venue'",
            "--yes",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);

        var patchMessages = api.Messages.Where(message => message.RequestType == nameof(PatchEntitiesRequest)).ToList();
        patchMessages.Should().HaveCount(2);
        patchMessages.Select(message => JObject.Parse(message.Data)[nameof(PatchEntitiesRequest.Requests)]!.Count())
            .Should().Equal(1000, 1);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("ndjson")]
    [InlineData("tsv")]
    public async Task Entities_update_structured_output_does_not_include_progress_text(string outputFormat)
    {
        var api = new CapturingCliApi { EntityResponses = [CreateEntity("venue-1", "Venue")] };
        using var serviceProvider = CreateServiceProvider(api);
        var rootCommand = CreateRootCommand(serviceProvider);

        var (exitCode, output) = await InvokeWithOutput(rootCommand, [
            "entities",
            "update",
            "Status",
            "Closed",
            "--where",
            "x.entityType='Venue'",
            "--yes",
            "--output",
            outputFormat
        ]);

        exitCode.Should().Be(0);
        output.Should().NotContain("Matching DataHub entities");
        output.Should().NotContain("Patching");
        output.Should().NotContain("Loading target entities");
    }

    private static ServiceProvider CreateServiceProvider(ICLIApi api)
    {
        var services = new ServiceCollection();
        services.AddSingleton(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private static RootCommand CreateRootCommand(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        return rootCommand;
    }

    private static async Task<(int ExitCode, string Output)> InvokeWithOutput(RootCommand rootCommand, string[] args)
    {
        await using var output = new StringWriter();
        var originalOutput = Console.Out;
        Console.SetOut(output);
        try
        {
            var exitCode = await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);
            return (exitCode, output.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    private static JObject CreateEntity(string id, string entityType, params (string Key, object? Value)[] properties)
    {
        var values = new Dictionary<string, object?>
        {
            [nameof(DataHubEntity.id)] = id,
            [nameof(DataHubEntity.entityType)] = entityType
        };

        foreach (var property in properties)
        {
            values[property.Key] = property.Value;
        }

        return JObject.FromObject(values);
    }

    private static string ToExpectedJson(string value)
    {
        try
        {
            JToken.Parse(value);
            return value;
        }
        catch (JsonReaderException)
        {
            return JsonConvert.SerializeObject(value);
        }
    }

    private static JObject GetPatchRequest(CapturingCliApi api)
        => JObject.Parse(api.Messages.Single(message => message.RequestType == nameof(PatchEntitiesRequest)).Data);

    private static void AssertPatchSideEffects(JObject patch, bool expectedSilent, bool expectedDispatchNotifications)
    {
        patch[nameof(PatchEntitiesRequest.Silent)]!.Value<bool>().Should().Be(expectedSilent);
        patch[nameof(PatchEntitiesRequest.DispatchNotifications)]!.Value<bool>().Should().Be(expectedDispatchNotifications);

        var requests = patch[nameof(PatchEntitiesRequest.Requests)]!.Values<JObject>().ToList();
        requests.Select(request => request![nameof(PatchEntityRequest.Silent)]!.Value<bool>()).Should().OnlyContain(value => value == expectedSilent);
        requests.Select(request => request![nameof(PatchEntityRequest.DispatchNotifications)]!.Value<bool>()).Should().OnlyContain(value => value == expectedDispatchNotifications);
    }

    private sealed class CapturingCliApi : ICLIApi
    {
        public List<SerializedRequest> Messages { get; } = [];
        public List<CliTargetOptions?> Targets { get; } = [];
        public List<JObject> EntityResponses { get; init; } = [];
        public List<List<JObject>> EntityResponsePages { get; init; } = [];
        public List<PatchEntityResponse>? PatchResponses { get; init; }

        private int _entityResponsePageIndex;

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            Targets.Add(CliTargetContext.Current);

            object response = message.RequestType switch
            {
                nameof(GetEntitiesWhereRequest) => CreateGetEntitiesWhereResponse(),
                nameof(GetEntitiesByIdRequest) => new GetEntitiesResponse
                {
                    Success = true,
                    Results = EntityResponses,
                    ResultCount = EntityResponses.Count,
                    MoreResultsAvailable = false
                },
                nameof(PatchEntitiesRequest) => CreatePatchEntitiesResponse(JObject.Parse(message.Data)[nameof(PatchEntitiesRequest.Requests)]!.Count()),
                _ => throw new InvalidOperationException($"No test response configured for {message.RequestType}.")
            };

            return Task.FromResult((T)response);
        }

        private GetEntitiesResponse CreateGetEntitiesWhereResponse()
        {
            if (EntityResponsePages.Count == 0)
            {
                return new GetEntitiesResponse
                {
                    Success = true,
                    Results = EntityResponses,
                    ResultCount = EntityResponses.Count,
                    MoreResultsAvailable = false
                };
            }

            var pageIndex = _entityResponsePageIndex++;
            var moreResultsAvailable = pageIndex < EntityResponsePages.Count - 1;
            return new GetEntitiesResponse
            {
                Success = true,
                Results = EntityResponsePages[pageIndex],
                ResultCount = EntityResponsePages.Sum(page => page.Count),
                ContinuationToken = moreResultsAvailable ? $"page-{pageIndex + 2}" : null!,
                MoreResultsAvailable = moreResultsAvailable
            };
        }

        private List<PatchEntityResponse> CreatePatchEntitiesResponse(int requestCount)
        {
            if (PatchResponses == null)
            {
                return Enumerable.Range(0, requestCount)
                    .Select(_ => new PatchEntityResponse { Success = true })
                    .ToList();
            }

            return PatchResponses.Take(requestCount).ToList();
        }
    }

    private sealed class PreviewRow
    {
        public int Matched { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? Property { get; set; }
        public JToken? CurrentValue { get; set; }
        public JToken? NewValue { get; set; }
    }

    private sealed class ResultRow
    {
        public int Matched { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
    }
}
