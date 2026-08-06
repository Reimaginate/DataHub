using System.CommandLine;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.CLI.Base.Profiles;
using Reimaginate.DataHub.CLI.Tools.Config;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Entities;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Merge.Failures;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Patch.Failures;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Sync.Failures;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.TrackingData;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Contexts;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class CommandRegistrationTests
{
    [Fact]
    public void AddDataHubCliCommands_registers_active_command_tree()
    {
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();

        rootCommand.AddDataHubTools(serviceProvider);

        rootCommand.Subcommands.Select(command => command.Name).Should().Contain([
            "auth",
            "setup",
            "login",
            "logout",
            "whoami",
            "entities",
            "data",
            "export",
            "jobs",
            "alternate-keys",
            "tracking",
            "source-entities",
            "logs",
            "diagnostics",
            "locks",
            "alerts",
            "resolution-promises",
            "sync-markers"
        ]);
        rootCommand.Subcommands.Select(command => command.Name).Should().NotContain("duplicates");
        rootCommand.Subcommands.Select(command => command.Name).Should().NotContain("export-data");
        rootCommand.Subcommands.Select(command => command.Name).Should().NotContain(["profiles", "users", "roles", "permissions", "sync", "merge", "patch"]);

        var auth = Find(rootCommand, "auth");
        auth.Subcommands.Select(command => command.Name).Should().Contain(["login", "logout", "whoami", "status", "users", "roles", "permissions"]);
        Find(auth, "users").Subcommands.Select(command => command.Name).Should().Contain(["list", "add", "update", "enable", "disable"]);
        Find(auth, "roles").Subcommands.Select(command => command.Name).Should().Contain(["list", "add", "update", "delete"]);
        Find(auth, "permissions").Subcommands.Select(command => command.Name).Should().Contain("list");
        Find(rootCommand, "entities").Subcommands.Select(command => command.Name).Should().BeEquivalentTo(["list", "get", "delete", "import", "patch", "update", "revert", "split-alt-key", "detach", "rebase", "find-by-alt-key", "counts"]);
        Find(rootCommand, "export").Subcommands.Should().BeEmpty();
        Find(rootCommand, "data").Subcommands.Select(command => command.Name).Should().Contain("duplicates");
        Find(rootCommand, "data").Subcommands.Select(command => command.Name).Should().NotContain("export");
        Find(Find(rootCommand, "data"), "duplicates").Subcommands.Select(command => command.Name).Should().Contain(["list", "get"]);
        Find(rootCommand, "source-entities").Subcommands.Select(command => command.Name).Should().Contain("rebase");
        Find(rootCommand, "jobs").Subcommands.Select(command => command.Name).Should().Contain(["list", "query", "get", "delete", "submit", "retry"]);
        Find(rootCommand, "jobs").Subcommands.Single(command => command.Name == "submit").Subcommands.Should().BeEmpty();
        Find(rootCommand, "logs").Subcommands.Select(command => command.Name).Should().Contain("query");
        Find(rootCommand, "diagnostics").Subcommands.Select(command => command.Name).Should().Contain("trace");
        Find(rootCommand, "locks").Subcommands.Select(command => command.Name).Should().Contain("list");
        Find(rootCommand, "alerts").Subcommands.Select(command => command.Name).Should().Contain(["list", "get"]);
        Find(rootCommand, "tracking").Subcommands.Select(command => command.Name).Should().Contain(["list", "delete"]);
        Find(rootCommand, "resolution-promises").Subcommands.Select(command => command.Name).Should().Contain(["list", "get", "patch", "delete", "resolve"]);
        Find(rootCommand, "sync-markers").Subcommands.Select(command => command.Name).Should().Contain(["list", "get", "patch"]);
    }

    [Fact]
    public async Task Setup_command_creates_and_selects_datahub_profile()
    {
        var store = new InMemoryProfileStore(new SharedProfileDocument());
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        services.AddSingleton<IProfileStore>(store);
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "setup",
            "--profile",
            "dev",
            "--url",
            "https://datahub.test/api/cli",
            "--tenant",
            "tenant-1",
            "--scope",
            "api://datahub-api/datahub_cli",
            "--yes"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        store.Document.CurrentProfile.Should().Be("dev");
        var profile = store.Document.Profiles.Should().ContainSingle(candidate => candidate.Name == "dev").Subject;
        var target = profile.Targets[DataHubContextDescriptor.ToolIdValue]!.AsObject();
        target["url"]!.GetValue<string>().Should().Be("https://datahub.test/api/cli");
        target["tenantId"]!.GetValue<string>().Should().Be("tenant-1");
        target["scope"]!.GetValue<string>().Should().Be("api://datahub-api/datahub_cli");
    }

    [Fact]
    public async Task Setup_command_uses_default_datahub_scope_when_scope_is_omitted()
    {
        var store = new InMemoryProfileStore(new SharedProfileDocument());
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        services.AddSingleton<IProfileStore>(store);
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "setup",
            "--profile",
            "dev",
            "--url",
            "https://datahub.test/api/cli",
            "--tenant",
            "tenant-1",
            "--yes"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        var target = store.Document.Profiles.Single().Targets[DataHubContextDescriptor.ToolIdValue]!.AsObject();
        target["scope"]!.GetValue<string>().Should().Be("api://7a3a7b0c-3f0b-43dd-b45f-487f1060ee91/datahub_cli");
    }

    [Fact]
    public async Task Entities_list_without_where_reports_active_usage()
    {
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        await using var error = new StringWriter();
        var originalError = Console.Error;

        Console.SetError(error);
        try
        {
            var exitCode = await rootCommand.Parse(["entities", "list"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(2);
            error.ToString().Should().Contain("datahub entities list <where> [options]");
            error.ToString().Should().NotContain("datahub entities list --where <where> [options]");
            error.ToString().Should().NotContain("get entities where");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Entities_list_help_shows_active_where_and_options()
    {
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        await using var output = new StringWriter();
        var originalOutput = Console.Out;

        Console.SetOut(output);
        try
        {
            var exitCode = await rootCommand.Parse(["entities", "list", "--help"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(0);
            output.ToString().Should().Contain("<where>");
            output.ToString().Should().Contain("--profile");
            output.ToString().Should().NotContain("--auth-provider");
            output.ToString().Should().Contain("--properties");
            output.ToString().Should().Contain("--output");
            output.ToString().Should().Contain("--page-size");
            output.ToString().Should().NotContain("--where");
            output.ToString().Should().NotContain("--patch");
            output.ToString().Should().NotContain("--ids");
            output.ToString().Should().NotContain("--ids-only");
            output.ToString().Should().NotContain("--sync-to");
            output.ToString().Should().NotContain("--merge-from");
            output.ToString().Should().NotContain("--rebase");
            output.ToString().Should().NotContain("Additional Arguments");
            output.ToString().Should().NotContain("get entities where");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public async Task Entities_detach_help_shows_required_arguments()
    {
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        await using var output = new StringWriter();
        var originalOutput = Console.Out;

        Console.SetOut(output);
        try
        {
            var exitCode = await rootCommand.Parse(["entities", "detach", "--help"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(0);
            output.ToString().Should().Contain("<where>");
            output.ToString().Should().Contain("<dataSource>");
            output.ToString().Should().NotContain("Additional Arguments");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public async Task Entities_detach_without_arguments_is_rejected_before_api_call()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse(["entities", "detach"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().NotBe(0);
        api.Message.Should().BeNull();
    }

    [Fact]
    public async Task Entities_split_alt_key_without_required_options_is_rejected_before_api_call()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse(["entities", "split-alt-key", "Contact", "contact-1"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().NotBe(0);
        api.Message.Should().BeNull();
    }

    [Fact]
    public async Task Active_commands_do_not_accept_auth_provider_option()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "list",
            "x.entityType='Venue'",
            "--auth-provider",
            "azure-cli",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().NotBe(0);
        api.Message.Should().BeNull();
    }

    [Fact]
    public async Task Entities_list_does_not_accept_where_option()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "list",
            "--where",
            "x.entityType='Venue'",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().NotBe(0);
        api.Message.Should().BeNull();
    }

    [Fact]
    public async Task Entities_list_does_not_accept_patch_option()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "list",
            "x.entityType='Venue'",
            "--patch",
            "patches.json",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().NotBe(0);
        api.Message.Should().BeNull();
    }

    [Fact]
    public void Legacy_get_entities_where_does_not_expose_patch_option()
    {
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var command = new GetEntitiesWhereCommand(serviceProvider);

        command.Options.Select(option => option.Name).Should().NotContain("patch");
        command.Options.SelectMany(option => option.Aliases).Should().NotContain("--patch");
    }

    [Fact]
    public async Task Profiles_command_is_not_registered_by_datahub_tools()
    {
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse(["profiles", "list"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task Active_commands_accept_context_as_profile_alias()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "list",
            "x.entityType='Venue'",
            "--context",
            "dev",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Message.Should().NotBeNull();
    }

    [Fact]
    public async Task Active_commands_apply_per_command_target_options()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "list",
            "x.entityType='Venue'",
            "--profile",
            "dev",
            "--url",
            "https://override.datahub.test/api/cli",
            "--tenant-id",
            "tenant-1",
            "--scope",
            "api://scope/datahub_cli",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Targets.Should().ContainSingle().Which.Should().Be(new CliTargetOptions(
            Context: "dev",
            Url: "https://override.datahub.test/api/cli",
            TenantId: "tenant-1",
            Scope: "api://scope/datahub_cli"));
    }

    [Fact]
    public async Task Entities_get_handles_null_results_response()
    {
        var api = new NullResultsCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        await using var output = new StringWriter();
        var originalOutput = Console.Out;

        Console.SetOut(output);
        try
        {
            var exitCode = await rootCommand.Parse([
                "entities",
                "get",
                "Contact",
                "88c04434-0747-4afd-b781-fb58bae676b9",
                "--output",
                "json"
            ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(0);
            api.Message.Should().NotBeNull();
            api.Message!.RequestType.Should().Be(nameof(GetEntitiesByIdRequest));
            var payload = JObject.Parse(api.Message.Data);
            payload[nameof(GetEntitiesByIdRequest.EntityType)]!.Value<string>().Should().Be("Contact");
            payload[nameof(GetEntitiesByIdRequest.EntityIds)]!.Values<string>().Should().Contain("88c04434-0747-4afd-b781-fb58bae676b9");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public void Legacy_command_handler_parameters_bind_to_declared_symbols()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(new CapturingCliApi());
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var failures = new List<string>();
        var commandTypes = typeof(GetEntitiesByIdCommand).Assembly
            .GetExportedTypes()
            .Where(type => typeof(Command).IsAssignableFrom(type) &&
                           !typeof(IDataHubTopLevelCommand).IsAssignableFrom(type) &&
                           type is { IsAbstract: false })
            .OrderBy(type => type.FullName)
            .ToList();

        foreach (var commandType in commandTypes)
        {
            var handler = commandType.GetMethod(
                "HandleCommand",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (handler == null)
            {
                continue;
            }

            var command = (Command)ActivatorUtilities.CreateInstance(serviceProvider, commandType);
            var symbolNames = command.Options
                .SelectMany(option => option.Aliases.Append(option.Name))
                .Concat(command.Arguments.Select(argument => argument.Name))
                .Select(NormalizeSymbolName)
                .ToHashSet();
            var parameterNames = handler.GetParameters()
                .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
                .Select(parameter => NormalizeSymbolName(parameter.Name ?? string.Empty))
                .ToHashSet();

            foreach (var parameter in handler.GetParameters())
            {
                if (parameter.ParameterType == typeof(CancellationToken))
                {
                    continue;
                }

                var parameterName = NormalizeSymbolName(parameter.Name ?? string.Empty);
                if (!symbolNames.Contains(parameterName))
                {
                    failures.Add($"{commandType.Name}.{handler.Name} parameter '{parameter.Name}' has no matching argument or option.");
                }
            }

            foreach (var argument in command.Arguments)
            {
                var argumentName = NormalizeSymbolName(argument.Name);
                if (!parameterNames.Contains(argumentName))
                {
                    failures.Add($"{commandType.Name} argument '{argument.Name}' has no matching {handler.Name} parameter.");
                }
            }

            foreach (var option in command.Options)
            {
                if (!option.Aliases.Append(option.Name).Select(NormalizeSymbolName).Any(parameterNames.Contains))
                {
                    failures.Add($"{commandType.Name} option '--{option.Name}' has no matching {handler.Name} parameter.");
                }
            }
        }

        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task Active_command_help_renders_for_every_registered_command()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(new CapturingCliApi());
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        var commandPaths = GetCommandPaths(rootCommand).ToList();
        var failures = new List<string>();
        await using var output = new StringWriter();
        await using var error = new StringWriter();
        var originalOutput = Console.Out;
        var originalError = Console.Error;

        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            foreach (var path in commandPaths)
            {
                var exitCode = await rootCommand.Parse([.. path, "--help"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);
                if (exitCode != 0)
                {
                    failures.Add($"{string.Join(" ", path)} --help exited with {exitCode}.");
                }
            }
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }

        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task Entities_ids_is_not_registered()
    {
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse(["entities", "ids", "x.entityType='Venue'"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task Removed_action_shortcuts_are_rejected_before_api_call()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        (await rootCommand.Parse(["entities", "sync"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().Be(1);
        (await rootCommand.Parse(["entities", "merge"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().Be(1);
        (await rootCommand.Parse(["entities", "list", "x.entityType='Venue'", "--detach", "source"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().NotBe(0);
        (await rootCommand.Parse(["entities", "list", "x.entityType='Venue'", "--rebase", "now"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().NotBe(0);
        (await rootCommand.Parse(["entities", "list", "x.entityType='Venue'", "--ids-only"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().NotBe(0);
        (await rootCommand.Parse(["sync", "failures", "list"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().Be(1);
        (await rootCommand.Parse(["merge", "failures", "list"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().Be(1);
        (await rootCommand.Parse(["tracking", "list", "source", "type", "id", "--delete"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().NotBe(0);
        (await rootCommand.Parse(["patch", "failures", "list"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().Be(1);
        (await rootCommand.Parse(["jobs", "submit", "duplicate-merge"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken)).Should().Be(2);
        api.Message.Should().BeNull();
    }

    [Fact]
    public void Legacy_read_commands_do_not_expose_removed_action_options()
    {
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();

        AssertDoesNotExposeOptions(new GetEntitiesWhereCommand(serviceProvider), "detach", "sync-to-data-source", "merge-from-data-source", "rebase", "rebase-source", "rebase-to", "ids-only");
        AssertDoesNotExposeOptions(new GetEntitiesByIdCommand(serviceProvider), "detach-from-data-source");
        AssertDoesNotExposeOptions(new GetSyncFailuresCommand(serviceProvider), "delete", "retry");
        AssertDoesNotExposeOptions(new GetSyncFailuresWhereCommand(serviceProvider), "delete", "retry", "detach", "rebase", "rebase-source");
        AssertDoesNotExposeOptions(new GetSyncFailuresByIdCommand(serviceProvider), "delete", "retry");
        AssertDoesNotExposeOptions(new GetMergeFailuresCommand(serviceProvider), "delete", "retry", "rebase", "rebase-source");
        AssertDoesNotExposeOptions(new GetMergeFailuresWhereCommand(serviceProvider), "delete", "retry", "rebase", "rebase-source");
        AssertDoesNotExposeOptions(new GetMergeFailuresByIdCommand(serviceProvider), "delete", "retry");
        AssertDoesNotExposeOptions(new GetPatchFailuresCommand(serviceProvider), "delete");
        AssertDoesNotExposeOptions(new GetPatchFailuresWhereCommand(serviceProvider), "delete", "retry", "rebase", "rebase-source");
        AssertDoesNotExposeOptions(new GetPatchFailuresByIdCommand(serviceProvider), "delete");
        AssertDoesNotExposeOptions(new GetTrackingDataCommand(serviceProvider), "delete");
    }

    [Fact]
    public async Task Entities_list_accepts_legacy_property_aliases()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "entities",
            "list",
            "x.entityType='Venue'",
            "--props",
            "id",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Message.Should().NotBeNull();
        api.Message!.RequestType.Should().Be(nameof(GetEntitiesWhereRequest));
        var payload = JObject.Parse(api.Message.Data);
        payload[nameof(GetEntitiesWhereRequest.WhereClause)]!.Value<string>().Should().Be("x.entityType='Venue'");
        payload[nameof(GetEntitiesWhereRequest.Select)]!.Value<string>().Should().Be("x.id");
        payload[nameof(GetEntitiesWhereRequest.PageSize)]!.Value<int>().Should().Be(50);
    }

    [Fact]
    public async Task Export_sends_get_entities_by_id_request()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        var outputPath = Path.Combine(Path.GetTempPath(), $"datahub-export-{Guid.NewGuid():N}.json");

        try
        {
            var exitCode = await rootCommand.Parse([
                "export",
                "Contact",
                "contact-1",
                "contact-2",
                "--save-to",
                outputPath
            ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(0);
            api.Message.Should().NotBeNull();
            api.Message!.RequestType.Should().Be(nameof(GetEntitiesByIdRequest));
            var payload = JObject.Parse(api.Message.Data);
            payload[nameof(GetEntitiesByIdRequest.EntityType)]!.Value<string>().Should().Be("Contact");
            payload[nameof(GetEntitiesByIdRequest.EntityIds)]!.Values<string>().Should().BeEquivalentTo(["contact-1", "contact-2"]);
            File.ReadAllText(outputPath).Should().Contain("[]");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task Diagnostic_and_job_commands_send_expected_requests()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        await InvokeAndAssert(rootCommand, api, ["locks", "list", "--output", "json"], nameof(RetrieveProcessingLocksRequest));

        await InvokeAndAssert(rootCommand, api, ["alerts", "list", "x.severity='Error'", "--output", "json"], nameof(GetAlertsWhereRequest));
        JObject.Parse(api.Message!.Data)[nameof(GetAlertsWhereRequest.WhereClause)]!.Value<string>().Should().Be("x.severity='Error'");

        await InvokeAndAssert(rootCommand, api, ["alerts", "get", "alert-1", "alert-2", "--output", "json"], nameof(GetAlertsByIdRequest));
        JObject.Parse(api.Message!.Data)[nameof(GetAlertsByIdRequest.Ids)]!.Values<string>().Should().BeEquivalentTo(["alert-1", "alert-2"]);

        await InvokeAndAssert(rootCommand, api, ["data", "duplicates", "list", "x.status='Open'", "--output", "json"], nameof(GetDuplicatesRequest));
        JObject.Parse(api.Message!.Data)[nameof(GetDuplicatesRequest.Where)]!.Value<string>().Should().Be("x.status='Open'");

        await InvokeAndAssert(rootCommand, api, ["data", "duplicates", "get", "duplicate-1", "--output", "json"], nameof(GetDuplicateRequest));
        JObject.Parse(api.Message!.Data)[nameof(GetDuplicateRequest.Id)]!.Value<string>().Should().Be("duplicate-1");

        await InvokeAndAssert(rootCommand, api, ["entities", "find-by-alt-key", "--key", "source.type", "--value", "source-id", "--output", "json"], nameof(GetEntitiesByAltKeyRequest));
        var altKeys = JObject.Parse(api.Message!.Data)[nameof(GetEntitiesByAltKeyRequest.AlternateKeys)]!;
        altKeys.First![nameof(AlternateKey.Key)]!.Value<string>().Should().Be("source.type");
        altKeys.First![nameof(AlternateKey.Value)]!.Value<string>().Should().Be("source-id");

        await InvokeAndAssert(rootCommand, api, ["entities", "counts", "x.entityType='Venue'", "--output", "json"], nameof(GetDataHubEntityTypeCountsRequest));
        JObject.Parse(api.Message!.Data)[nameof(GetDataHubEntityTypeCountsRequest.WhereClause)]!.Value<string>().Should().Be("x.entityType='Venue'");

        await InvokeAndAssert(rootCommand, api, [
            "entities",
            "revert",
            "Contact",
            "contact-1",
            "contact-2",
            "--at",
            "2026-06-07T10:30:00+10:00",
            "--yes",
            "--output",
            "json"
        ], nameof(RevertDataHubEntitiesRequest));
        var revertByTime = JObject.Parse(api.Message!.Data);
        revertByTime[nameof(RevertDataHubEntitiesRequest.EntityType)]!.Value<string>().Should().Be("Contact");
        revertByTime[nameof(RevertDataHubEntitiesRequest.EntityIds)]!.Values<string>().Should().BeEquivalentTo(["contact-1", "contact-2"]);
        revertByTime[nameof(RevertDataHubEntitiesRequest.RevertTo)]!.ToObject<DateTimeOffset>().Should().Be(DateTimeOffset.Parse("2026-06-07T10:30:00+10:00"));
        revertByTime[nameof(RevertDataHubEntitiesRequest.DispatchNotifications)]!.Value<bool>().Should().BeTrue();

        await InvokeAndAssert(rootCommand, api, [
            "entities",
            "revert",
            "--tracking-entry-id",
            "tracking-1",
            "--silent-notifications",
            "--dry-run",
            "--output",
            "json"
        ], nameof(RevertDataHubEntitiesRequest));
        var revertByTrackingEntry = JObject.Parse(api.Message!.Data);
        revertByTrackingEntry[nameof(RevertDataHubEntitiesRequest.TrackingEntryId)]!.Value<string>().Should().Be("tracking-1");
        revertByTrackingEntry[nameof(RevertDataHubEntitiesRequest.DispatchNotifications)]!.Value<bool>().Should().BeFalse();
        revertByTrackingEntry[nameof(RevertDataHubEntitiesRequest.DryRun)]!.Value<bool>().Should().BeTrue();

        await InvokeAndAssert(rootCommand, api, [
            "entities",
            "split-alt-key",
            "Contact",
            "contact-1",
            "--key",
            "source.type",
            "--value",
            "source-id",
            "--silent",
            "--dry-run",
            "--output",
            "json"
        ], nameof(SplitDataHubEntityAlternateKeyRequest));
        var splitAltKey = JObject.Parse(api.Message!.Data);
        splitAltKey[nameof(SplitDataHubEntityAlternateKeyRequest.EntityType)]!.Value<string>().Should().Be("Contact");
        splitAltKey[nameof(SplitDataHubEntityAlternateKeyRequest.EntityId)]!.Value<string>().Should().Be("contact-1");
        splitAltKey[nameof(SplitDataHubEntityAlternateKeyRequest.Key)]!.Value<string>().Should().Be("source.type");
        splitAltKey[nameof(SplitDataHubEntityAlternateKeyRequest.Value)]!.Value<string>().Should().Be("source-id");
        splitAltKey[nameof(SplitDataHubEntityAlternateKeyRequest.Silent)]!.Value<bool>().Should().BeTrue();
        splitAltKey[nameof(SplitDataHubEntityAlternateKeyRequest.DryRun)]!.Value<bool>().Should().BeTrue();

        await InvokeAndAssert(rootCommand, api, ["jobs", "query", "x.status='Failed'", "--output", "json"], nameof(GetJobsRequest));
        JObject.Parse(api.Message!.Data)[nameof(GetJobsRequest.Where)]!.Value<string>().Should().Be("x.status='Failed'");

        await InvokeAndAssert(rootCommand, api, ["jobs", "get", "job-1", "--output", "json"], nameof(GetJobRequest));
        JObject.Parse(api.Message!.Data)[nameof(GetJobRequest.JobId)]!.Value<string>().Should().Be("job-1");

        await InvokeAndAssert(rootCommand, api, ["jobs", "retry", "job-1", "--output", "json"], nameof(RetryJobRequest));
        JObject.Parse(api.Message!.Data)[nameof(RetryJobRequest.JobId)]!.Value<string>().Should().Be("job-1");
    }

    [Fact]
    public async Task Resolution_promises_commands_send_expected_requests()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        await InvokeAndAssert(rootCommand, api, [
            "resolution-promises",
            "list",
            "x.DataHubEntityType = 'Contact'",
            "--page-size",
            "25",
            "--output",
            "json"
        ], nameof(ListResolutionPromisesRequest));

        var list = JObject.Parse(api.Message!.Data);
        list[nameof(ListResolutionPromisesRequest.WhereClause)]!.Value<string>().Should().Be("x.DataHubEntityType = 'Contact'");
        list[nameof(ListResolutionPromisesRequest.PageSize)]!.Value<int>().Should().Be(25);

        await InvokeAndAssert(rootCommand, api, [
            "resolution-promises",
            "get",
            "promise-1",
            "promise-2",
            "--output",
            "json"
        ], nameof(GetResolutionPromisesRequest));

        var get = JObject.Parse(api.Message!.Data);
        get[nameof(GetResolutionPromisesRequest.PromiseIds)]!.Values<string>().Should().BeEquivalentTo(["promise-1", "promise-2"]);

        await InvokeAndAssert(rootCommand, api, [
            "resolution-promises",
            "patch",
            "promise-1",
            "ExternalEntityReference.EntityId",
            "\"source-2\"",
            "--yes",
            "--output",
            "json"
        ], nameof(PatchResolutionPromiseRequest));

        var patch = JObject.Parse(api.Message!.Data);
        patch[nameof(PatchResolutionPromiseRequest.PromiseId)]!.Value<string>().Should().Be("promise-1");
        var patchOperation = patch[nameof(PatchResolutionPromiseRequest.Operations)]!.Should().ContainSingle().Subject;
        patchOperation[nameof(Patch.Operation)]!.Value<string>().Should().Be("set");
        patchOperation[nameof(Patch.Path)]!.Value<string>().Should().Be("ExternalEntityReference.EntityId");
        patchOperation[nameof(Patch.Value)]!.Value<string>().Should().Be("source-2");

        await InvokeAndAssert(rootCommand, api, [
            "resolution-promises",
            "delete",
            "--where",
            "x.DataHubEntityType = 'Contact'",
            "--dry-run",
            "--output",
            "json"
        ], nameof(DeleteResolutionPromisesRequest));

        var delete = JObject.Parse(api.Message!.Data);
        delete[nameof(DeleteResolutionPromisesRequest.WhereClause)]!.Value<string>().Should().Be("x.DataHubEntityType = 'Contact'");
        delete[nameof(DeleteResolutionPromisesRequest.DryRun)]!.Value<bool>().Should().BeTrue();
        delete[nameof(DeleteResolutionPromisesRequest.PageSize)]!.Value<int>().Should().Be(500);

        await InvokeAndAssert(rootCommand, api, [
            "resolution-promises",
            "resolve",
            "--ids",
            "promise-1",
            "promise-2",
            "--page-size",
            "25",
            "--yes",
            "--do-not-track",
            "--stop-on-failure",
            "--profile",
            "dev",
            "--url",
            "https://datahub.test/api/cli",
            "--tenant-id",
            "tenant-1",
            "--scope",
            "api://scope/datahub_cli",
            "--output",
            "json"
        ], nameof(ResolveResolutionPromisesRequest));

        var byIds = JObject.Parse(api.Message!.Data);
        byIds[nameof(ResolveResolutionPromisesRequest.PromiseIds)]!.Values<string>().Should().BeEquivalentTo(["promise-1", "promise-2"]);
        byIds[nameof(ResolveResolutionPromisesRequest.PageSize)]!.Value<int>().Should().Be(25);
        byIds[nameof(ResolveResolutionPromisesRequest.DoNotTrack)]!.Value<bool>().Should().BeTrue();
        byIds[nameof(ResolveResolutionPromisesRequest.StopOnFailure)]!.Value<bool>().Should().BeTrue();
        api.Targets.Last().Should().Be(new CliTargetOptions(
            Context: "dev",
            Url: "https://datahub.test/api/cli",
            TenantId: "tenant-1",
            Scope: "api://scope/datahub_cli"));

        await InvokeAndAssert(rootCommand, api, [
            "resolution-promises",
            "resolve",
            "--where",
            "x.DataHubEntityType = 'Contact'",
            "--dry-run",
            "--output",
            "json"
        ], nameof(ResolveResolutionPromisesRequest));

        var byWhere = JObject.Parse(api.Message!.Data);
        byWhere[nameof(ResolveResolutionPromisesRequest.WhereClause)]!.Value<string>().Should().Be("x.DataHubEntityType = 'Contact'");
        byWhere[nameof(ResolveResolutionPromisesRequest.PageSize)]!.Value<int>().Should().Be(500);
        byWhere[nameof(ResolveResolutionPromisesRequest.DryRun)]!.Value<bool>().Should().BeTrue();
        byWhere[nameof(ResolveResolutionPromisesRequest.StopOnFailure)]!.Value<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Resolution_promises_resolve_chunks_explicit_ids_to_the_safe_page_size()
    {
        var api = new BatchedResolutionPromisesCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        var promiseIds = Enumerable.Range(1, 1001).Select(index => $"promise-{index}").ToList();
        var arguments = new List<string> { "resolution-promises", "resolve", "--ids" };
        arguments.AddRange(promiseIds);
        arguments.AddRange(["--dry-run", "--output", "json"]);

        var exitCode = await rootCommand.Parse(arguments.ToArray()).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Messages.Should().HaveCount(3);
        api.Messages.Select(message => message.CorrelationId).Distinct()
            .Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
        var requests = api.Messages.Select(message => JObject.Parse(message.Data)).ToList();
        requests.Select(payload => payload[nameof(ResolveResolutionPromisesRequest.PageSize)]!.Value<int>())
            .Should().OnlyContain(pageSize => pageSize == 500);
        requests.Select(payload => payload[nameof(ResolveResolutionPromisesRequest.PromiseIds)]!.Count())
            .Should().Equal(500, 500, 1);
        requests.SelectMany(payload => payload[nameof(ResolveResolutionPromisesRequest.PromiseIds)]!.Values<string>())
            .Should().Equal(promiseIds);
        requests.Select(payload => payload[nameof(ResolveResolutionPromisesRequest.ContinuationToken)]?.Value<string>())
            .Should().OnlyContain(continuationToken => string.IsNullOrWhiteSpace(continuationToken));
    }

    [Fact]
    public async Task Resolution_promises_resolve_follows_continuation_tokens_within_an_explicit_id_batch()
    {
        var api = new PagedResolutionPromisesCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "resolution-promises",
            "resolve",
            "--ids",
            "promise-1",
            "promise-2",
            "--dry-run",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Messages.Should().HaveCount(2);
        var first = JObject.Parse(api.Messages[0].Data);
        var second = JObject.Parse(api.Messages[1].Data);
        first[nameof(ResolveResolutionPromisesRequest.ContinuationToken)]?.Value<string>().Should().BeNullOrEmpty();
        second[nameof(ResolveResolutionPromisesRequest.ContinuationToken)]!.Value<string>().Should().Be("page-2");
        first[nameof(ResolveResolutionPromisesRequest.PromiseIds)]!.Values<string>().Should().Equal("promise-1", "promise-2");
        second[nameof(ResolveResolutionPromisesRequest.PromiseIds)]!.Values<string>().Should().Equal("promise-1", "promise-2");
    }

    [Fact]
    public async Task Resolution_promises_resolve_restarts_after_mutation_then_advances_past_unresolved_pages()
    {
        var api = new MutatingPagedResolutionPromisesCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        var output = new StringWriter();
        var originalOutput = Console.Out;
        var originalAnsiConsole = AnsiConsole.Console;

        Console.SetOut(output);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new FixedWidthAnsiConsoleOutput(output)
        });
        try
        {
            var exitCode = await rootCommand.Parse([
                "resolution-promises",
                "resolve",
                "--where",
                "x.DataHubEntityType = 'Contact'",
                "--yes",
                "--output",
                "json"
            ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(0);
        }
        finally
        {
            AnsiConsole.Console = originalAnsiConsole;
            Console.SetOut(originalOutput);
        }

        api.Messages.Should().HaveCount(3);
        var requests = api.Messages.Select(message => JObject.Parse(message.Data)).ToList();
        requests[0][nameof(ResolveResolutionPromisesRequest.ContinuationToken)]?.Value<string>().Should().BeNullOrEmpty();
        requests[1][nameof(ResolveResolutionPromisesRequest.ContinuationToken)]?.Value<string>().Should().BeNullOrEmpty();
        requests[2][nameof(ResolveResolutionPromisesRequest.ContinuationToken)]!.Value<string>().Should().Be("page-2");
        output.ToString().Split("\"PromiseId\": \"promise-unresolved-1\"").Length.Should().Be(2);
    }

    [Theory]
    [InlineData("delete", nameof(DeleteResolutionPromisesRequest))]
    [InlineData("resolve", nameof(ResolveResolutionPromisesRequest))]
    public async Task Resolution_promises_where_operations_page_from_cli(string subcommand, string expectedRequestType)
    {
        var api = new PagedResolutionPromisesCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var args = new List<string>
        {
            "resolution-promises",
            subcommand,
            "--where",
            "x.DataHubEntityType = 'Contact'",
            "--dry-run",
            "--output",
            "json"
        };
        if (subcommand == "resolve")
        {
            args.Insert(args.IndexOf("--dry-run"), "--stop-on-failure");
        }

        var exitCode = await rootCommand.Parse(args.ToArray()).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Messages.Should().HaveCount(2);
        api.Messages.Should().OnlyContain(message => message.RequestType == expectedRequestType);

        var first = JObject.Parse(api.Messages[0].Data);
        var second = JObject.Parse(api.Messages[1].Data);
        first["ContinuationToken"]?.Value<string>().Should().BeNullOrEmpty();
        second["ContinuationToken"]!.Value<string>().Should().Be("page-2");
        second["WhereClause"]!.Value<string>().Should().Be("x.DataHubEntityType = 'Contact'");
        if (expectedRequestType == nameof(ResolveResolutionPromisesRequest))
        {
            first[nameof(ResolveResolutionPromisesRequest.StopOnFailure)]!.Value<bool>().Should().BeTrue();
            second[nameof(ResolveResolutionPromisesRequest.StopOnFailure)]!.Value<bool>().Should().BeTrue();
        }
    }

    [Fact]
    public async Task Resolution_promises_delete_where_pages_from_cli_for_confirmed_delete()
    {
        var api = new PagedResolutionPromisesCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "resolution-promises",
            "delete",
            "--where",
            "x.DataHubEntityType = 'Contact'",
            "--yes",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Messages.Should().HaveCount(2);
        api.Messages.Should().OnlyContain(message => message.RequestType == nameof(DeleteResolutionPromisesRequest));

        var first = JObject.Parse(api.Messages[0].Data);
        var second = JObject.Parse(api.Messages[1].Data);
        first[nameof(DeleteResolutionPromisesRequest.WhereClause)]!.Value<string>().Should().Be("x.DataHubEntityType = 'Contact'");
        first[nameof(DeleteResolutionPromisesRequest.ContinuationToken)]?.Value<string>().Should().BeNullOrEmpty();
        first[nameof(DeleteResolutionPromisesRequest.DryRun)]!.Value<bool>().Should().BeFalse();
        first[nameof(DeleteResolutionPromisesRequest.PageSize)]!.Value<int>().Should().Be(500);
        second[nameof(DeleteResolutionPromisesRequest.WhereClause)]!.Value<string>().Should().Be("x.DataHubEntityType = 'Contact'");
        second[nameof(DeleteResolutionPromisesRequest.ContinuationToken)]?.Value<string>().Should().BeNullOrEmpty();
        second[nameof(DeleteResolutionPromisesRequest.DryRun)]!.Value<bool>().Should().BeFalse();
        second[nameof(DeleteResolutionPromisesRequest.PageSize)]!.Value<int>().Should().Be(500);
    }

    [Fact]
    public async Task Resolution_promises_delete_where_stops_when_page_only_reports_already_deleted_results()
    {
        var api = new AlreadyDeletedResolutionPromisesCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "resolution-promises",
            "delete",
            "--where",
            "x.DataHubEntityType = 'Contact'",
            "--yes",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Messages.Should().ContainSingle();
    }

    [Theory]
    [InlineData("list", "PromiseId,DataHubEntityId", null, new[] { "PromiseId", "DataHubEntityId" }, new[] { "DataHubEntityType", "DataSource" })]
    [InlineData("get", null, "TargetEntityType", new[] { "PromiseId", "DataSource", "TargetEntityType" }, new string[] { })]
    [InlineData("delete", "PromiseId,Status", null, new[] { "PromiseId", "Status" }, new[] { "Reason", "DataHubEntityType" })]
    [InlineData("resolve", null, "Reason", new[] { "PromiseId", "ResolvedEntityId", "Reason" }, new string[] { })]
    [InlineData("patch", "PromiseId,Changed,Result.DataSource", null, new[] { "PromiseId", "Changed", "Result.DataSource" }, new[] { "Status", "Result.DataHubEntityId" })]
    public async Task Resolution_promises_output_commands_support_display_properties(
        string subcommand,
        string? properties,
        string? additionalProperties,
        string[] expectedOutput,
        string[] unexpectedOutput)
    {
        var api = new ResolutionPromiseOutputCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        var output = new StringWriter();
        var originalOutput = Console.Out;
        var originalAnsiConsole = AnsiConsole.Console;

        Console.SetOut(output);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new FixedWidthAnsiConsoleOutput(output)
        });
        try
        {
            var args = ResolutionPromiseOutputArgs(subcommand, properties, additionalProperties);
            var exitCode = await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(0);
        }
        finally
        {
            AnsiConsole.Console = originalAnsiConsole;
            Console.SetOut(originalOutput);
        }

        var rendered = output.ToString();
        foreach (var expected in expectedOutput)
        {
            rendered.Should().Contain(expected);
        }

        foreach (var unexpected in unexpectedOutput)
        {
            rendered.Should().NotContain(unexpected);
        }
    }

    [Fact]
    public async Task Resolution_promises_resolve_where_table_progress_escapes_markup_sensitive_values()
    {
        var api = new MarkupSensitiveResolutionPromisesCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "resolution-promises",
            "resolve",
            "--where",
            "x.DataHubEntityId='d847f696-767f-4365-bcc9-2791ba15e917'",
            "--dry-run"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task Resolution_promises_resolve_where_table_progress_handles_empty_results()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "resolution-promises",
            "resolve",
            "--where",
            "x.DataHubEntityId='d847f696-767f-4365-bcc9-2791ba15e917'",
            "--dry-run"
        ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Message.Should().NotBeNull();
    }

    [Fact]
    public async Task Resolution_promises_resolve_where_stops_after_current_page_when_cancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var api = new CancellingResolutionPromisesCliApi(cancellation);
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse([
            "resolution-promises",
            "resolve",
            "--where",
            "x.DataHubEntityType = 'Contact'",
            "--dry-run",
            "--output",
            "json"
        ]).InvokeAsync(new InvocationConfiguration(), cancellation.Token);

        exitCode.Should().Be(CliExitCodes.Cancelled);
        api.Messages.Should().ContainSingle();
    }

    [Theory]
    [InlineData("resolution-promises", "resolve", "--output", "json")]
    [InlineData("resolution-promises", "resolve", "--ids", "promise-1", "--where", "x.id = 'promise-2'", "--yes", "--output", "json")]
    [InlineData("resolution-promises", "delete", "--output", "json")]
    [InlineData("resolution-promises", "delete", "--ids", "promise-1", "--where", "x.id = 'promise-2'", "--yes", "--output", "json")]
    public async Task Resolution_promises_rejects_invalid_target_modes(params string[] args)
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(2);
        api.Message.Should().BeNull();
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("resolve")]
    public async Task Resolution_promises_destructive_commands_cancel_when_warning_is_not_confirmed(string subcommand)
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        await using var error = new StringWriter();
        using var input = new StringReader("N");
        var originalError = Console.Error;
        var originalInput = Console.In;

        Console.SetError(error);
        Console.SetIn(input);
        try
        {
            var exitCode = await rootCommand.Parse([
                "resolution-promises",
                subcommand,
                "--ids",
                "promise-1",
                "--output",
                "json"
            ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(CliExitCodes.Cancelled);
            api.Message.Should().BeNull();
            error.ToString().Should().Contain("WARNING:");
            error.ToString().Should().Contain("Type Y to continue");
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetIn(originalInput);
        }
    }

    [Theory]
    [InlineData("delete", nameof(DeleteResolutionPromisesRequest))]
    [InlineData("resolve", nameof(ResolveResolutionPromisesRequest))]
    public async Task Resolution_promises_destructive_commands_continue_when_warning_is_confirmed(string subcommand, string expectedRequestType)
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        await using var error = new StringWriter();
        using var input = new StringReader("Y");
        var originalError = Console.Error;
        var originalInput = Console.In;

        Console.SetError(error);
        Console.SetIn(input);
        try
        {
            await InvokeAndAssert(rootCommand, api, [
                "resolution-promises",
                subcommand,
                "--ids",
                "promise-1",
                "--output",
                "json"
            ], expectedRequestType);

            error.ToString().Should().Contain("WARNING:");
            error.ToString().Should().Contain("Type Y to continue");
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetIn(originalInput);
        }
    }

    [Fact]
    public async Task Resolution_promises_patch_cancels_when_warning_is_not_confirmed()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        await using var error = new StringWriter();
        using var input = new StringReader("N");
        var originalError = Console.Error;
        var originalInput = Console.In;

        Console.SetError(error);
        Console.SetIn(input);
        try
        {
            var exitCode = await rootCommand.Parse([
                "resolution-promises",
                "patch",
                "promise-1",
                "EntityReferencePath",
                "UpdatedPath",
                "--output",
                "json"
            ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(CliExitCodes.Cancelled);
            api.Message.Should().BeNull();
            error.ToString().Should().Contain("WARNING:");
            error.ToString().Should().Contain("Type Y to continue");
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetIn(originalInput);
        }
    }

    [Fact]
    public async Task Entities_resolve_promises_path_is_removed()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse(["entities", "resolve-promises", "--ids", "promise-1", "--dry-run", "--output", "json"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().NotBe(0);
        api.Message.Should().BeNull();
    }

    [Fact]
    public async Task Sync_markers_list_sends_get_request()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        await InvokeAndAssert(rootCommand, api, ["sync-markers", "list", "--output", "json"], nameof(GetSyncMarkersRequest));
    }

    [Fact]
    public async Task Sync_markers_list_forwards_where_and_page_size()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        await InvokeAndAssert(rootCommand, api, [
            "sync-markers",
            "list",
            "x.DataSource = 'SRC1'",
            "--page-size",
            "50",
            "--output",
            "json"
        ], nameof(GetSyncMarkersRequest));

        var payload = JObject.Parse(api.Message!.Data);
        payload[nameof(GetSyncMarkersRequest.WhereClause)]!.Value<string>().Should().Be("x.DataSource = 'SRC1'");
        payload[nameof(GetSyncMarkersRequest.PageSize)]!.Value<int>().Should().Be(50);
    }

    [Fact]
    public async Task Sync_markers_get_sends_parameterized_id_query()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        await InvokeAndAssert(rootCommand, api, ["sync-markers", "get", "marker-1", "--output", "json"], nameof(GetSyncMarkersRequest));

        var payload = JObject.Parse(api.Message!.Data);
        payload[nameof(GetSyncMarkersRequest.WhereClause)]!.Value<string>().Should().Be("x.id = @id");
        payload[nameof(GetSyncMarkersRequest.Parameters)]!.Single()![nameof(DataHubQueryParameter.Value)]!.Value<string>().Should().Be("marker-1");
    }

    [Fact]
    public async Task Sync_markers_patch_sends_patch_request_when_confirmed_by_option()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        await InvokeAndAssert(rootCommand, api, [
            "sync-markers",
            "patch",
            "marker-1",
            "--value",
            "abc",
            "--yes",
            "--output",
            "json"
        ], nameof(PatchSyncMarkerRequest));

        var payload = JObject.Parse(api.Message!.Data);
        payload[nameof(PatchSyncMarkerRequest.MarkerId)]!.Value<string>().Should().Be("marker-1");
        payload[nameof(PatchSyncMarkerRequest.Value)]!.Value<string>().Should().Be("abc");
        payload[nameof(PatchSyncMarkerRequest.UpdateValue)]!.Value<bool>().Should().BeTrue();
        payload[nameof(PatchSyncMarkerRequest.UpdateLastRunTime)]!.Value<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Sync_markers_patch_without_fields_is_rejected_before_api_call()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var exitCode = await rootCommand.Parse(["sync-markers", "patch", "marker-1", "--output", "json"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(CliExitCodes.Usage);
        api.Message.Should().BeNull();
    }

    [Fact]
    public async Task Sync_markers_patch_cancels_when_warning_is_not_confirmed()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        await using var error = new StringWriter();
        using var input = new StringReader("N");
        var originalError = Console.Error;
        var originalInput = Console.In;

        Console.SetError(error);
        Console.SetIn(input);
        try
        {
            var exitCode = await rootCommand.Parse([
                "sync-markers",
                "patch",
                "marker-1",
                "--value",
                "abc",
                "--output",
                "json"
            ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(CliExitCodes.Cancelled);
            api.Message.Should().BeNull();
            error.ToString().Should().Contain("WARNING:");
            error.ToString().Should().Contain("Type Y to continue");
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetIn(originalInput);
        }
    }

    [Fact]
    public async Task Logs_query_sends_raw_log_entry_request()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        await InvokeAndAssert(rootCommand, api, [
            "logs",
            "query",
            "x.Type in ('SyncFailure','MergeFailure')",
            "--properties",
            "id,Type,Timestamp",
            "--page-size",
            "50",
            "--output",
            "json"
        ], nameof(GetLogEntriesWhereRequest));

        var payload = JObject.Parse(api.Message!.Data);
        payload[nameof(GetLogEntriesWhereRequest.WhereClause)]!.Value<string>().Should().Be("x.Type in ('SyncFailure','MergeFailure')");
        payload[nameof(GetLogEntriesWhereRequest.PageSize)]!.Value<int>().Should().Be(50);
    }

    [Fact]
    public async Task Diagnostics_trace_sends_get_trace_request()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        await InvokeAndAssert(rootCommand, api, [
            "diagnostics",
            "trace",
            "corr-1",
            "--lookback-hours",
            "6",
            "--from",
            "2026-06-20T00:00:00Z",
            "--to",
            "2026-06-21T00:00:00Z",
            "--limit",
            "50",
            "--include-logs",
            "--output",
            "json"
        ], nameof(GetTraceRequest));

        var payload = JObject.Parse(api.Message!.Data);
        payload[nameof(GetTraceRequest.TraceCorrelationId)]!.Value<string>().Should().Be("corr-1");
        payload[nameof(GetTraceRequest.LookbackHours)]!.Value<int>().Should().Be(6);
        payload[nameof(GetTraceRequest.Limit)]!.Value<int>().Should().Be(50);
        payload[nameof(GetTraceRequest.IncludeLogs)]!.Value<bool>().Should().BeTrue();
        var request = JsonConvert.DeserializeObject<GetTraceRequest>(api.Message.Data)!;
        request.FromUtc.Should().Be(DateTimeOffset.Parse("2026-06-20T00:00:00Z"));
        request.ToUtc.Should().Be(DateTimeOffset.Parse("2026-06-21T00:00:00Z"));
    }

    [Fact]
    public async Task Auth_access_management_commands_send_expected_requests()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        await InvokeAndAssert(rootCommand, api, ["auth", "users", "list", "--output", "json"], nameof(GetUsersRequest));

        await InvokeAndAssert(rootCommand, api, [
            "auth",
            "users",
            "add",
            "--name",
            "Admin User",
            "--email",
            "admin@contoso.test",
            "--tenant-id",
            "tenant-1",
            "--object-id",
            "object-1",
            "--roles",
            "Admin",
            "--output",
            "json"
        ], nameof(RegisterUserRequest));
        var userRequest = JObject.Parse(api.Message!.Data);
        userRequest[nameof(RegisterUserRequest.Name)]!.Value<string>().Should().Be("Admin User");
        userRequest[nameof(RegisterUserRequest.TenantId)]!.Value<string>().Should().Be("tenant-1");

        await InvokeAndAssert(rootCommand, api, [
            "auth",
            "roles",
            "add",
            "--name",
            "Operators",
            "--permissions",
            "query:users",
            "--description",
            "Operator role",
            "--output",
            "json"
        ], nameof(RegisterRoleRequest));
        var roleRequest = JObject.Parse(api.Message!.Data);
        roleRequest[nameof(RegisterRoleRequest.Name)]!.Value<string>().Should().Be("Operators");
        roleRequest[nameof(RegisterRoleRequest.Permissions)]!.Values<string>().Should().Contain("query:users");

        await InvokeAndAssert(rootCommand, api, ["auth", "permissions", "list", "--output", "json"], nameof(GetPermissionsRequest));
    }

    [Fact]
    public async Task Active_command_runtime_failure_returns_failure_exit_code()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(new ThrowingCliApi());
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);
        await using var error = new StringWriter();
        var originalError = Console.Error;

        Console.SetError(error);
        try
        {
            var exitCode = await rootCommand.Parse([
                "entities",
                "list",
                "x.entityType='Venue'",
                "--output",
                "json"
            ]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

            exitCode.Should().Be(1);
            error.ToString().Should().Contain("Error: API unavailable");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }


    private static Command Find(Command command, string name)
    {
        return command.Subcommands.Single(subcommand => subcommand.Name == name);
    }

    private static void AssertDoesNotExposeOptions(Command command, params string[] optionNames)
    {
        var names = command.Options.Select(option => option.Name).ToList();
        var aliases = command.Options.SelectMany(option => option.Aliases).ToList();

        foreach (var optionName in optionNames)
        {
            names.Should().NotContain(optionName);
            aliases.Should().NotContain($"--{optionName}");
        }
    }

    private static string NormalizeSymbolName(string value)
        => value
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .TrimStart('-')
            .ToLowerInvariant();

    private static IEnumerable<string[]> GetCommandPaths(Command command, string[]? prefix = null)
    {
        prefix ??= [];

        foreach (var subcommand in command.Subcommands)
        {
            var path = prefix.Concat([subcommand.Name]).ToArray();
            yield return path;

            foreach (var childPath in GetCommandPaths(subcommand, path))
            {
                yield return childPath;
            }
        }
    }

    private static string[] ResolutionPromiseOutputArgs(string subcommand, string? properties, string? additionalProperties)
    {
        var args = subcommand switch
        {
            "list" => new List<string> { "resolution-promises", "list" },
            "get" => new List<string> { "resolution-promises", "get", "promise-1" },
            "delete" => new List<string> { "resolution-promises", "delete", "--ids", "promise-1", "--dry-run" },
            "resolve" => new List<string> { "resolution-promises", "resolve", "--ids", "promise-1", "--dry-run" },
            "patch" => new List<string> { "resolution-promises", "patch", "promise-1", "DataSource", "SRC2", "--dry-run" },
            _ => throw new ArgumentOutOfRangeException(nameof(subcommand), subcommand, "Unsupported resolution promise output command.")
        };

        if (!string.IsNullOrWhiteSpace(properties))
        {
            args.Add("--props");
            args.Add(properties);
        }

        if (!string.IsNullOrWhiteSpace(additionalProperties))
        {
            args.Add("--add-props");
            args.Add(additionalProperties);
        }

        return args.ToArray();
    }

    private static async Task InvokeAndAssert(Command rootCommand, CapturingCliApi api, string[] args, string requestType)
    {
        api.Clear();
        var exitCode = await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        api.Message.Should().NotBeNull();
        api.Message!.RequestType.Should().Be(requestType);
    }

    private sealed class CapturingCliApi : ICLIApi
    {
        public SerializedRequest? Message { get; private set; }
        public List<CliTargetOptions?> Targets { get; } = [];

        public void Clear()
        {
            Message = null;
        }

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Message = message;
            Targets.Add(CliTargetContext.Current);
            object response = typeof(T) switch
            {
                var type when type == typeof(GetEntitiesResponse) => new GetEntitiesResponse { Success = true, Results = [], MoreResultsAvailable = false },
                var type when type == typeof(GetSyncFailuresResponse) => new GetSyncFailuresResponse { Success = true, Results = [], MoreResultsAvailable = false },
                var type when type == typeof(GetSyncMarkersResponse) => new GetSyncMarkersResponse { Success = true, Results = [] },
                var type when type == typeof(GetMergeFailuresResponse) => new GetMergeFailuresResponse { Success = true, Results = [], MoreResultsAvailable = false },
                var type when type == typeof(GetPatchFailuresResponse) => new GetPatchFailuresResponse { Success = true, Results = [], MoreResultsAvailable = false },
                var type when type == typeof(GetTrackingDataResponse) => new GetTrackingDataResponse { Success = true, Results = [], MoreResultsAvailable = false },
                var type when type == typeof(RevertDataHubEntitiesResponse) => new RevertDataHubEntitiesResponse { Results = [] },
                var type when type == typeof(SplitDataHubEntityAlternateKeyResponse) => new SplitDataHubEntityAlternateKeyResponse { Success = true },
                var type when type == typeof(GetLogEntriesResponse) => new GetLogEntriesResponse { Success = true, Results = [], MoreResultsAvailable = false },
                var type when type == typeof(RetrieveProcessingLocksResponse) => new RetrieveProcessingLocksResponse { Success = true, ProcessingLocks = [] },
                var type when type == typeof(GetAlertsResponse) => new GetAlertsResponse { Results = [], MoreResultsAvailable = false },
                var type when type == typeof(GetDuplicatesResponse) => new GetDuplicatesResponse { Success = true, Results = [], MoreResultsAvailable = false },
                var type when type == typeof(GetDuplicateResponse) => new GetDuplicateResponse { Success = true },
                var type when type == typeof(GetDataHubEntityTypeCountsResponse) => new GetDataHubEntityTypeCountsResponse { Success = true, Results = [] },
                var type when type == typeof(GetJobsResponse) => new GetJobsResponse { Success = true, Results = [], MoreResultsAvailable = false },
                var type when type == typeof(GetJobResponse) => new GetJobResponse { Success = true },
                var type when type == typeof(RetryJobResponse) => new RetryJobResponse { Success = true },
                var type when type == typeof(ListResolutionPromisesResponse) => new ListResolutionPromisesResponse { Results = [] },
                var type when type == typeof(GetResolutionPromisesResponse) => new GetResolutionPromisesResponse { Results = [] },
                var type when type == typeof(PatchResolutionPromiseResponse) => new PatchResolutionPromiseResponse { PromiseId = "promise-1", Success = true },
                var type when type == typeof(PatchSyncMarkerResponse) => new PatchSyncMarkerResponse { MarkerId = "marker-1", Success = true, Result = new SyncMarker { id = "marker-1" } },
                var type when type == typeof(DeleteResolutionPromisesResponse) => new DeleteResolutionPromisesResponse { Results = [] },
                var type when type == typeof(ResolveResolutionPromisesResponse) => new ResolveResolutionPromisesResponse { Results = [] },
                var type when type == typeof(GetUsersResponse) => new GetUsersResponse { Success = true, Results = [] },
                var type when type == typeof(RegisterUserResponse) => new RegisterUserResponse { Success = true },
                var type when type == typeof(GetRolesResponse) => new GetRolesResponse { Success = true, Results = [] },
                var type when type == typeof(RegisterRoleResponse) => new RegisterRoleResponse { Success = true },
                var type when type == typeof(GetPermissionsResponse) => new GetPermissionsResponse { Success = true, Results = [] },
                var type when type == typeof(GetTraceResponse) => new GetTraceResponse
                {
                    Success = true,
                    CorrelationId = "corr-1",
                    FromUtc = DateTimeOffset.Parse("2026-06-20T00:00:00Z"),
                    ToUtc = DateTimeOffset.Parse("2026-06-21T00:00:00Z"),
                    Records = []
                },
                _ => throw new InvalidOperationException($"No test response configured for {typeof(T).Name}.")
            };

            return Task.FromResult((T)response);
        }
    }

    private sealed class ResolutionPromiseOutputCliApi : ICLIApi
    {
        private static readonly ResolutionPromiseResult Promise = new()
        {
            PromiseId = "promise-1",
            DataHubEntityType = "OwnerType",
            DataHubEntityId = "owner-1",
            EntityReferencePath = "Parent.Child",
            DataSource = "SRC",
            SourceEntityType = "SourceType",
            SourceEntityId = "source-1",
            TargetEntityType = "TargetType"
        };

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            object response = typeof(T) switch
            {
                var type when type == typeof(ListResolutionPromisesResponse) => new ListResolutionPromisesResponse
                {
                    Results = [Promise]
                },
                var type when type == typeof(GetResolutionPromisesResponse) => new GetResolutionPromisesResponse
                {
                    Results = [Promise]
                },
                var type when type == typeof(DeleteResolutionPromisesResponse) => new DeleteResolutionPromisesResponse
                {
                    MatchedCount = 1,
                    Results =
                    [
                        new DeleteResolutionPromiseResult
                        {
                            PromiseId = Promise.PromiseId,
                            DataHubEntityType = Promise.DataHubEntityType,
                            DataHubEntityId = Promise.DataHubEntityId,
                            EntityReferencePath = Promise.EntityReferencePath,
                            DataSource = Promise.DataSource,
                            SourceEntityType = Promise.SourceEntityType,
                            SourceEntityId = Promise.SourceEntityId,
                            TargetEntityType = Promise.TargetEntityType,
                            Status = "DryRun",
                            Reason = "Matched but not deleted."
                        }
                    ]
                },
                var type when type == typeof(ResolveResolutionPromisesResponse) => new ResolveResolutionPromisesResponse
                {
                    MatchedCount = 1,
                    Results =
                    [
                        new ResolveResolutionPromiseResult
                        {
                            PromiseId = Promise.PromiseId,
                            DataHubEntityType = Promise.DataHubEntityType,
                            DataHubEntityId = Promise.DataHubEntityId,
                            EntityReferencePath = Promise.EntityReferencePath,
                            TargetEntityType = Promise.TargetEntityType,
                            ResolvedEntityId = "target-1",
                            Status = "Resolved",
                            Reason = "Resolved."
                        }
                    ]
                },
                var type when type == typeof(PatchResolutionPromiseResponse) => new PatchResolutionPromiseResponse
                {
                    PromiseId = Promise.PromiseId,
                    Success = true,
                    Changed = true,
                    Status = "DryRun",
                    Reason = "Matched but not patched.",
                    Result = Promise
                },
                _ => throw new InvalidOperationException($"No test response configured for {typeof(T).Name}.")
            };

            return Task.FromResult((T)response);
        }
    }

    private sealed class FixedWidthAnsiConsoleOutput(TextWriter writer) : IAnsiConsoleOutput
    {
        public TextWriter Writer { get; } = writer;
        public bool IsTerminal => false;
        public int Width => 300;
        public int Height => 100;

        public void SetEncoding(System.Text.Encoding encoding)
        {
        }
    }

    private sealed class PagedResolutionPromisesCliApi : ICLIApi
    {
        public List<SerializedRequest> Messages { get; } = [];

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            var payload = JObject.Parse(message.Data);
            var continuationToken = payload.Value<string>("ContinuationToken");
            var currentRequestCount = Messages.Count(sent => sent.RequestType == message.RequestType);
            var moreResults = message.RequestType == nameof(DeleteResolutionPromisesRequest) && payload.Value<bool>("DryRun") == false
                ? currentRequestCount == 1
                : string.IsNullOrWhiteSpace(continuationToken);
            object response = typeof(T) switch
            {
                var type when type == typeof(DeleteResolutionPromisesResponse) => new DeleteResolutionPromisesResponse
                {
                    MatchedCount = 1,
                    DeletedCount = payload.Value<bool>("DryRun") ? 0 : 1,
                    ContinuationToken = moreResults ? "page-2" : null,
                    MoreResultsAvailable = moreResults,
                    Results = []
                },
                var type when type == typeof(ResolveResolutionPromisesResponse) => new ResolveResolutionPromisesResponse
                {
                    MatchedCount = 1,
                    ContinuationToken = moreResults ? "page-2" : null,
                    MoreResultsAvailable = moreResults,
                    Results = []
                },
                _ => throw new InvalidOperationException($"No test response configured for {typeof(T).Name}.")
            };

            return Task.FromResult((T)response);
        }
    }

    private sealed class BatchedResolutionPromisesCliApi : ICLIApi
    {
        public List<SerializedRequest> Messages { get; } = [];

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            object response = typeof(T) == typeof(ResolveResolutionPromisesResponse)
                ? new ResolveResolutionPromisesResponse { MoreResultsAvailable = false, Results = [] }
                : throw new InvalidOperationException($"No test response configured for {typeof(T).Name}.");

            return Task.FromResult((T)response);
        }
    }

    private sealed class MutatingPagedResolutionPromisesCliApi : ICLIApi
    {
        public List<SerializedRequest> Messages { get; } = [];

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            object response = Messages.Count switch
            {
                1 => new ResolveResolutionPromisesResponse
                {
                    MatchedCount = 2,
                    ResolvedCount = 1,
                    UnresolvedCount = 1,
                    MoreResultsAvailable = true,
                    ContinuationToken = "stale-page-token",
                    Results =
                    [
                        new ResolveResolutionPromiseResult { PromiseId = "promise-resolved", Status = "Resolved" },
                        new ResolveResolutionPromiseResult { PromiseId = "promise-unresolved-1", Status = "Unresolved" }
                    ]
                },
                2 => new ResolveResolutionPromisesResponse
                {
                    MatchedCount = 1,
                    UnresolvedCount = 1,
                    MoreResultsAvailable = true,
                    ContinuationToken = "page-2",
                    Results =
                    [
                        new ResolveResolutionPromiseResult { PromiseId = "promise-unresolved-1", Status = "Unresolved" }
                    ]
                },
                3 => new ResolveResolutionPromisesResponse
                {
                    MatchedCount = 1,
                    UnresolvedCount = 1,
                    MoreResultsAvailable = false,
                    Results =
                    [
                        new ResolveResolutionPromiseResult { PromiseId = "promise-unresolved-2", Status = "Unresolved" }
                    ]
                },
                _ => throw new InvalidOperationException("Resolve should complete after restarting once and advancing once.")
            };

            return Task.FromResult((T)response);
        }
    }

    private sealed class AlreadyDeletedResolutionPromisesCliApi : ICLIApi
    {
        public List<SerializedRequest> Messages { get; } = [];

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            if (Messages.Count > 1)
            {
                throw new InvalidOperationException("Delete should stop when a page reports no new delete progress.");
            }

            object response = typeof(T) == typeof(DeleteResolutionPromisesResponse)
                ? new DeleteResolutionPromisesResponse
                {
                    MatchedCount = 1,
                    DeletedCount = 1,
                    MoreResultsAvailable = true,
                    ContinuationToken = "page-2",
                    Results =
                    [
                        new DeleteResolutionPromiseResult
                        {
                            PromiseId = "promise-1",
                            Status = "Deleted",
                            Reason = "Already deleted."
                        }
                    ]
                }
                : throw new InvalidOperationException($"No test response configured for {typeof(T).Name}.");

            return Task.FromResult((T)response);
        }
    }

    private sealed class MarkupSensitiveResolutionPromisesCliApi : ICLIApi
    {
        public List<SerializedRequest> Messages { get; } = [];

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            object response = typeof(T) == typeof(ResolveResolutionPromisesResponse)
                ? new ResolveResolutionPromisesResponse
                {
                    MatchedCount = 1,
                    UnresolvedCount = 1,
                    MoreResultsAvailable = false,
                    Results =
                    [
                        new ResolveResolutionPromiseResult
                        {
                            PromiseId = "promise-[1]",
                            Status = "Unresolved",
                            Reason = "No [target] found.",
                            DataHubEntityType = "Owner[Type]",
                            DataHubEntityId = "owner-[1]",
                            EntityReferencePath = "Parent[0]",
                            TargetEntityType = "Target[Type]"
                        }
                    ]
                }
                : throw new InvalidOperationException($"No test response configured for {typeof(T).Name}.");

            return Task.FromResult((T)response);
        }
    }

    private sealed class CancellingResolutionPromisesCliApi(CancellationTokenSource cancellation) : ICLIApi
    {
        public List<SerializedRequest> Messages { get; } = [];

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            Messages.Add(message);
            if (Messages.Count > 1)
            {
                throw new InvalidOperationException("Resolve should stop before requesting another page after cancellation.");
            }

            object response = typeof(T) == typeof(ResolveResolutionPromisesResponse)
                ? new ResolveResolutionPromisesResponse
                {
                    MatchedCount = 1,
                    MoreResultsAvailable = true,
                    ContinuationToken = "page-2",
                    Results =
                    [
                        new ResolveResolutionPromiseResult
                        {
                            PromiseId = "promise-1",
                            Status = "Unresolved"
                        }
                    ]
                }
                : throw new InvalidOperationException($"No test response configured for {typeof(T).Name}.");

            cancellation.Cancel();
            return Task.FromResult((T)response);
        }
    }

    private sealed class ThrowingCliApi : ICLIApi
    {
        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("API unavailable");
        }
    }

    private sealed class NullResultsCliApi : ICLIApi
    {
        public SerializedRequest? Message { get; private set; }

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Message = message;
            object response = typeof(T) == typeof(GetEntitiesResponse)
                ? new GetEntitiesResponse { Success = true, Results = null!, MoreResultsAvailable = false }
                : throw new InvalidOperationException($"No test response configured for {typeof(T).Name}.");

            return Task.FromResult((T)response);
        }
    }

    private sealed class InMemoryProfileStore : IProfileStore
    {
        public InMemoryProfileStore(SharedProfileDocument document)
        {
            Document = document;
        }

        public SharedProfileDocument Document { get; private set; }
        public string StorePath => "memory://profiles.json";

        public Task<SharedProfileDocument> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Document);

        public Task WriteAsync(SharedProfileDocument document, CancellationToken cancellationToken = default)
        {
            Document = document;
            return Task.CompletedTask;
        }
    }
}
