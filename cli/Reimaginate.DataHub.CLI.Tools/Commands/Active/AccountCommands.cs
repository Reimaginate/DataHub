using System.CommandLine;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.CLI.Tools.Commands.Active;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.Auth;
using Reimaginate.DataHub.CLI.Tools.Shared.Models;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.CLI.Tools.Shared.Services.Connections;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

public sealed class LoginCommand : DataHubTopLevelCommand
{
    public LoginCommand(IServiceProvider serviceProvider) : base("login", serviceProvider)
    {
        AccountCommandActions.ConfigureLogin(this, serviceProvider);
    }
}

public sealed class LogoutCommand : DataHubTopLevelCommand
{
    public LogoutCommand(IServiceProvider serviceProvider) : base("logout", serviceProvider)
    {
        AccountCommandActions.ConfigureLogout(this, serviceProvider);
    }
}

public sealed class WhoamiCommand : DataHubTopLevelCommand
{
    public WhoamiCommand(IServiceProvider serviceProvider) : base("whoami", serviceProvider)
    {
        AccountCommandActions.ConfigureWhoami(this, serviceProvider);
    }
}

internal static class AccountCommandActions
{
    public static Command CreateLoginCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("login");
        ConfigureLogin(command, serviceProvider);
        return command;
    }

    public static Command CreateLogoutCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("logout");
        ConfigureLogout(command, serviceProvider);
        return command;
    }

    public static Command CreateWhoamiCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("whoami");
        ConfigureWhoami(command, serviceProvider);
        return command;
    }

    public static void ConfigureLogin(Command command, IServiceProvider serviceProvider)
    {
        command.Description = "Sign in to DataHub for the selected profile.";
        CommandContextOptions.AddTargetOptions(command);
        command.SetAction(async (parseResult, cancellationToken) =>
            await InvokeWithTargetAsync(serviceProvider, parseResult, cancellationToken, LoginAsync));
    }

    public static void ConfigureLogout(Command command, IServiceProvider serviceProvider)
    {
        command.Description = "Clear DataHub sign-in state for the selected profile.";
        CommandContextOptions.AddTargetOptions(command);
        command.SetAction(async (parseResult, cancellationToken) =>
            await InvokeWithTargetAsync(serviceProvider, parseResult, cancellationToken, LogoutAsync));
    }

    public static void ConfigureWhoami(Command command, IServiceProvider serviceProvider)
    {
        command.Description = "Show the signed-in DataHub identity for the selected profile.";
        CommandContextOptions.AddTargetOptions(command);
        command.SetAction(async (parseResult, cancellationToken) =>
            await InvokeWithTargetAsync(serviceProvider, parseResult, cancellationToken, WhoamiAsync));
    }

    public static async Task<int> InvokeWithTargetAsync(
        IServiceProvider serviceProvider,
        ParseResult parseResult,
        CancellationToken cancellationToken,
        Func<DataHubConnection, IDataHubCliTokenProvider, CancellationToken, Task<int>> action)
    {
        var previousTarget = CliTargetContext.Current;
        CliTargetContext.Current = CommandContextOptions.CreateTargetOptions(parseResult);
        try
        {
            var connection = await serviceProvider.GetRequiredService<IConnectionsService>().ResolveConnectionAsync(cancellationToken);
            var tokenProvider = serviceProvider.GetRequiredService<IDataHubCliTokenProvider>();
            return await action(connection, tokenProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]DataHub authentication command failed: {ex.Message}[/]");
            return CliExitCodes.Failure;
        }
        finally
        {
            CliTargetContext.Current = previousTarget;
        }
    }

    private static async Task<int> LoginAsync(DataHubConnection connection, IDataHubCliTokenProvider tokenProvider, CancellationToken cancellationToken)
    {
        connection.AccessToken = await tokenProvider.LoginAsync(connection, cancellationToken);
        AnsiConsole.MarkupLineInterpolated($"[green]MSAL login complete for DataHub profile '{connection.Name}' using scope '{connection.Scope}'.[/]");
        return CliExitCodes.Success;
    }

    private static async Task<int> LogoutAsync(DataHubConnection connection, IDataHubCliTokenProvider tokenProvider, CancellationToken cancellationToken)
    {
        await tokenProvider.LogoutAsync(connection, cancellationToken);
        AnsiConsole.MarkupLineInterpolated($"[green]MSAL cached sign-in state cleared for DataHub profile '{connection.Name}'.[/]");
        return CliExitCodes.Success;
    }

    private static async Task<int> WhoamiAsync(DataHubConnection connection, IDataHubCliTokenProvider tokenProvider, CancellationToken cancellationToken)
    {
        connection.AccessToken = await tokenProvider.GetAccessTokenSilentAsync(connection, cancellationToken);
        var claims = DecodeJwtClaims(connection.AccessToken);

        var table = new Table()
            .AddColumn("Field")
            .AddColumn("Value");

        AddRow(table, "Profile", connection.Name);
        AddRow(table, "Provider", "MSAL");
        AddRow(table, "User", FirstClaim(claims, "preferred_username", "upn", "email", "unique_name", "name"));
        AddRow(table, "Name", FirstClaim(claims, "name"));
        AddRow(table, "Object Id", FirstClaim(claims, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier"));
        AddRow(table, "Tenant", FirstClaim(claims, "tid", "tenantid"));
        AddRow(table, "Scope", FirstClaim(claims, "scp", "roles"));

        AnsiConsole.Write(table);
        return CliExitCodes.Success;
    }

    private static Dictionary<string, string> DecodeJwtClaims(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length < 2)
        {
            throw new InvalidOperationException("The acquired access token is not a valid JWT.");
        }

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            claims[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Array => string.Join(" ", property.Value.EnumerateArray().Select(element => element.ToString())),
                _ => property.Value.ToString()
            };
        }

        return claims;
    }

    private static string FirstClaim(IReadOnlyDictionary<string, string> claims, params string[] names)
    {
        foreach (var name in names)
        {
            if (claims.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static void AddRow(Table table, string field, string value)
        => table.AddRow(Markup.Escape(field), Markup.Escape(string.IsNullOrWhiteSpace(value) ? "-" : value));
}
