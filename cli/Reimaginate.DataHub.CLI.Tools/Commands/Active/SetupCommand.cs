using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.CLI.Base.Profiles;
using Reimaginate.DataHub.CLI.Tools.Commands.Active;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.Auth;
using Reimaginate.DataHub.CLI.Tools.Shared.Contexts;
using Reimaginate.DataHub.CLI.Tools.Shared.Models;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

public sealed class SetupCommand : DataHubTopLevelCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Option<string?> _profileOption = new("--profile") { Description = "Shared Reimaginate CLI profile to create or update." };
    private readonly Option<string?> _urlOption = new("--url") { Description = "DataHub CLI endpoint URL." };
    private readonly Option<string?> _tenantOption = new("--tenant") { Description = "Microsoft Entra tenant id." };
    private readonly Option<string?> _scopeOption = new("--scope") { Description = "DataHub API delegated scope." };
    private readonly Option<bool> _yesOption = new("--yes") { Description = "Overwrite an existing DataHub target without prompting." };
    private readonly Option<bool> _loginOption = new("--login") { Description = "Start browser sign-in after saving the profile." };

    public SetupCommand(IServiceProvider serviceProvider) : base("setup", serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Description = "Set up a DataHub profile.";
        _profileOption.Aliases.Add("--context");
        _tenantOption.Aliases.Add("--tenant-id");
        Add(_profileOption);
        Add(_urlOption);
        Add(_tenantOption);
        Add(_scopeOption);
        Add(_yesOption);
        Add(_loginOption);
        SetAction(InvokeAsync);
    }

    private async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            var store = _serviceProvider.GetRequiredService<IProfileStore>();
            var descriptor = _serviceProvider.GetServices<IToolProfileDescriptor>()
                .FirstOrDefault(candidate => candidate.ToolId == DataHubContextDescriptor.ToolIdValue)
                ?? new DataHubContextDescriptor();

            var document = await store.ReadAsync(cancellationToken);
            document.Profiles ??= [];

            var profileName = GetValueOrPrompt(
                parseResult,
                _profileOption,
                "Profile name",
                string.IsNullOrWhiteSpace(document.CurrentProfile) ? "default" : document.CurrentProfile)
                ?? "default";
            var profile = document.Profiles.FirstOrDefault(candidate => string.Equals(candidate.Name, profileName, StringComparison.OrdinalIgnoreCase));
            if (profile is null)
            {
                profile = new SharedProfile { Name = profileName };
                document.Profiles.Add(profile);
            }

            profile.Targets ??= [];
            var hasExistingTarget = profile.Targets.ContainsKey(DataHubContextDescriptor.ToolIdValue);
            if (hasExistingTarget && !parseResult.GetValue(_yesOption) &&
                !AnsiConsole.Confirm($"Replace DataHub settings for profile [yellow]{Markup.Escape(profileName)}[/]?"))
            {
                AnsiConsole.MarkupLine("[yellow]DataHub setup cancelled.[/]");
                return CliExitCodes.Usage;
            }

            var optionValues = new Dictionary<string, string?>
            {
                ["url"] = GetValueOrPrompt(parseResult, _urlOption, "DataHub CLI endpoint URL", GetExistingValue(profile, "url")),
                ["tenantId"] = GetValueOrPrompt(parseResult, _tenantOption, "Microsoft Entra tenant id", GetExistingValue(profile, "tenantId")),
                ["scope"] = GetValueOrPrompt(parseResult, _scopeOption, "DataHub API delegated scope", GetExistingValue(profile, "scope"), required: false)
            };

            var target = descriptor.CreateTarget(optionValues);
            var validation = descriptor.ValidateTarget(target);
            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{error}[/]");
                }

                return CliExitCodes.Usage;
            }

            profile.Targets[DataHubContextDescriptor.ToolIdValue] = target;
            document.CurrentProfile = profile.Name;
            await store.WriteAsync(document, cancellationToken);

            AnsiConsole.MarkupLineInterpolated($"[green]DataHub profile '{profile.Name}' saved and selected.[/]");
            AnsiConsole.MarkupLineInterpolated($"[grey]Profile store: {store.StorePath}[/]");

            if (!parseResult.GetValue(_loginOption))
            {
                return CliExitCodes.Success;
            }

            var connection = DataHubContextDescriptor.CreateConnection(new ResolvedToolProfileTarget(
                profile.Name,
                DataHubContextDescriptor.ToolIdValue,
                target,
                descriptor));
            var tokenProvider = _serviceProvider.GetRequiredService<IDataHubCliTokenProvider>();
            connection.AccessToken = await tokenProvider.LoginAsync(connection, cancellationToken);
            AnsiConsole.MarkupLineInterpolated($"[green]MSAL login complete for DataHub profile '{connection.Name}' using scope '{connection.Scope}'.[/]");
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]DataHub setup failed: {ex.Message}[/]");
            return CliExitCodes.Failure;
        }
    }

    private static string? GetExistingValue(SharedProfile profile, string fieldName)
        => profile.Targets?[DataHubContextDescriptor.ToolIdValue]?[fieldName]?.GetValue<string>();

    private static string? GetValueOrPrompt(
        ParseResult parseResult,
        Option<string?> option,
        string prompt,
        string? defaultValue = null,
        bool required = true)
    {
        var value = parseResult.GetValue(option);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!required)
        {
            return defaultValue;
        }

        var textPrompt = new TextPrompt<string>($"{prompt}:");
        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            textPrompt.DefaultValue(defaultValue);
        }

        return AnsiConsole.Prompt(textPrompt);
    }
}
