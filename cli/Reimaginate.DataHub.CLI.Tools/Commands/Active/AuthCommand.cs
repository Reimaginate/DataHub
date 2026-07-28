using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.CLI.Tools.Commands.Delete.Role;
using Reimaginate.DataHub.CLI.Tools.Commands.Disable.User;
using Reimaginate.DataHub.CLI.Tools.Commands.Enable.User;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Permissions;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Roles;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Users;
using Reimaginate.DataHub.CLI.Tools.Commands.Register.Role;
using Reimaginate.DataHub.CLI.Tools.Commands.Register.User;
using Reimaginate.DataHub.CLI.Tools.Commands.Update.Role;
using Reimaginate.DataHub.CLI.Tools.Commands.Update.User;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.Auth;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.CLI.Tools.Shared.Services.Connections;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

public sealed class AuthCommand : DataHubTopLevelCommand
{
    public AuthCommand(IServiceProvider serviceProvider) : base("auth", serviceProvider)
    {
        Description = "Manage DataHub authentication and access.";
        Add(AccountCommandActions.CreateLoginCommand(serviceProvider));
        Add(AccountCommandActions.CreateLogoutCommand(serviceProvider));
        Add(AccountCommandActions.CreateWhoamiCommand(serviceProvider));
        Add(CreateStatusCommand(serviceProvider));
        Add(CreateUsersCommand(serviceProvider));
        Add(CreateRolesCommand(serviceProvider));
        Add(CreatePermissionsCommand(serviceProvider));
    }

    private static Command CreateStatusCommand(IServiceProvider serviceProvider)
    {
        var status = new Command("status", "Check token readiness for the selected DataHub target.");
        var interactiveOption = new Option<bool>("--interactive") { Description = "Allow interactive sign-in if no cached token is available." };
        CommandContextOptions.AddTargetOptions(status);
        status.Add(interactiveOption);
        status.SetAction(async (parseResult, cancellationToken) =>
        {
            var previousTarget = CliTargetContext.Current;
            CliTargetContext.Current = CommandContextOptions.CreateTargetOptions(parseResult);
            try
            {
                var connection = await serviceProvider.GetRequiredService<IConnectionsService>().ResolveConnectionAsync(cancellationToken);
                var tokenProvider = serviceProvider.GetRequiredService<IDataHubCliTokenProvider>();
                connection.AccessToken = parseResult.GetValue(interactiveOption)
                    ? await tokenProvider.GetAccessTokenAsync(connection, cancellationToken)
                    : await tokenProvider.GetAccessTokenSilentAsync(connection, cancellationToken);

                AnsiConsole.MarkupLineInterpolated($"[green]MSAL token acquired for DataHub target '{connection.Name}' using scope '{connection.Scope}'.[/]");
                return CliExitCodes.Success;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]DataHub authentication is not ready: {ex.Message}[/]");
                return CliExitCodes.Failure;
            }
            finally
            {
                CliTargetContext.Current = previousTarget;
            }
        });
        return status;
    }

    private static Command CreateUsersCommand(IServiceProvider serviceProvider)
    {
        var users = new Command("users", "Manage DataHub users.");
        users.Add(UsersList(serviceProvider));
        users.Add(UserAdd(serviceProvider));
        users.Add(UserUpdate(serviceProvider));
        users.Add(UserEnable(serviceProvider));
        users.Add(UserDisable(serviceProvider));
        return users;
    }

    private static Command UsersList(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("list", "List users.", out var output);
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        ActiveCommandFactory.Add(command, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetUsersCommand(services).HandleCommand(parse.GetValue(properties), parse.GetValue(additional), ct)));
        return command;
    }

    private static Command UserAdd(IServiceProvider services)
        => UserUpsert("add", "Add a user.", (parse, ct) =>
            new RegisterUserCommand(services).HandleCommand(
                parse.GetValue<string>("--name")!,
                parse.GetValue<string>("--email")!,
                parse.GetValue<string>("--tenant-id")!,
                parse.GetValue<string>("--object-id")!,
                parse.GetValue<string?>("--upn"),
                parse.GetValue<string>("--roles")!,
                ct));

    private static Command UserUpdate(IServiceProvider services)
    {
        var command = UserUpsert("update", "Update a user.", (parse, ct) =>
            new UpdateUserCommand(services).HandleCommand(
                parse.GetValue<string>("id")!,
                parse.GetValue<string>("--name")!,
                parse.GetValue<string>("--email")!,
                parse.GetValue<string>("--tenant-id")!,
                parse.GetValue<string>("--object-id")!,
                parse.GetValue<string?>("--upn"),
                parse.GetValue<string>("--roles")!,
                ct));
        command.Add(ActiveCommandFactory.RequiredArgument("id", "DataHub user id."));
        return command;
    }

    private static Command UserUpsert(string name, string description, Func<ParseResult, CancellationToken, Task<int>> action)
    {
        var command = ActiveCommandFactory.NewCommand(name, description, out var output, includeTenantIdAlias: false);
        var nameOption = ActiveCommandFactory.StringOption("--name", "Name of the user.");
        nameOption.Required = true;
        var emailOption = ActiveCommandFactory.StringOption("--email", "Email address of the user.");
        emailOption.Required = true;
        var tenantOption = ActiveCommandFactory.StringOption("--tenant-id", "Microsoft Entra tenant id.");
        tenantOption.Required = true;
        var objectOption = ActiveCommandFactory.StringOption("--object-id", "Microsoft Entra object id.");
        objectOption.Required = true;
        var upnOption = ActiveCommandFactory.StringOption("--upn", "Azure tenant UPN, if different from email.");
        var rolesOption = ActiveCommandFactory.StringOption("--roles", "Comma-delimited list of roles.");
        rolesOption.Required = true;
        ActiveCommandFactory.Add(command, nameOption, emailOption, tenantOption, objectOption, upnOption, rolesOption);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () => action(parse, ct)));
        return command;
    }

    private static Command UserEnable(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("enable", "Enable a user.", out var output);
        var id = ActiveCommandFactory.RequiredArgument("id", "User id.");
        command.Add(id);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new EnableUserCommand(services).HandleCommand(parse.GetValue(id)!, ct)));
        return command;
    }

    private static Command UserDisable(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("disable", "Disable a user.", out var output);
        var id = ActiveCommandFactory.RequiredArgument("id", "User id.");
        command.Add(id);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new DisableUserCommand(services).HandleCommand(parse.GetValue(id)!, ct)));
        return command;
    }

    private static Command CreateRolesCommand(IServiceProvider serviceProvider)
    {
        var roles = new Command("roles", "Manage DataHub roles.");
        roles.Add(RolesList(serviceProvider));
        roles.Add(RoleAdd(serviceProvider));
        roles.Add(RoleUpdate(serviceProvider));
        roles.Add(RoleDelete(serviceProvider));
        return roles;
    }

    private static Command RolesList(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("list", "List roles.", out var output);
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        ActiveCommandFactory.Add(command, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetRolesCommand(services).HandleCommand(parse.GetValue(properties), parse.GetValue(additional), ct)));
        return command;
    }

    private static Command RoleAdd(IServiceProvider services)
        => RoleUpsert("add", "Add a role.", (parse, ct) =>
            new RegisterRoleCommand(services).HandleCommand(parse.GetValue<string>("--name")!, parse.GetValue<string>("--permissions")!, parse.GetValue<string?>("--description"), ct));

    private static Command RoleUpdate(IServiceProvider services)
    {
        var command = RoleUpsert("update", "Update a role.", (parse, ct) =>
            new UpdateRoleCommand(services).HandleCommand(parse.GetValue<string>("name")!, parse.GetValue<string>("--permissions")!, parse.GetValue<string?>("--description"), ct));
        command.Add(ActiveCommandFactory.RequiredArgument("name", "Role name."));
        return command;
    }

    private static Command RoleUpsert(string name, string description, Func<ParseResult, CancellationToken, Task<int>> action)
    {
        var command = ActiveCommandFactory.NewCommand(name, description, out var output);
        var nameOption = ActiveCommandFactory.StringOption("--name", "Name of the role.");
        if (name == "add")
        {
            nameOption.Required = true;
            command.Add(nameOption);
        }
        var permissions = ActiveCommandFactory.StringOption("--permissions", "Comma-delimited list of permissions.");
        permissions.Required = true;
        var roleDescription = ActiveCommandFactory.StringOption("--description", "Description of the role.");
        ActiveCommandFactory.Add(command, permissions, roleDescription);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () => action(parse, ct)));
        return command;
    }

    private static Command RoleDelete(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("delete", "Delete a role.", out var output);
        var name = ActiveCommandFactory.RequiredArgument("name", "Role name.");
        command.Add(name);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new DeleteRoleCommand(services).HandleCommand(parse.GetValue(name)!, ct)));
        return command;
    }

    private static Command CreatePermissionsCommand(IServiceProvider serviceProvider)
    {
        var permissions = new Command("permissions", "Inspect DataHub permissions.");
        var list = ActiveCommandFactory.NewCommand("list", "List permissions.", out var output);
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        ActiveCommandFactory.Add(list, properties, additional);
        list.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetPermissionsCommand(serviceProvider).HandleCommand(parse.GetValue(properties), parse.GetValue(additional), ct)));
        permissions.Add(list);
        return permissions;
    }
}
