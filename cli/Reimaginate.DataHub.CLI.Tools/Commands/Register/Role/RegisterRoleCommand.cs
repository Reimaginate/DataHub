using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Register.Role;

[Option("name", typeof(string), required: true, description: "Name of the role")]
[Option("permissions", typeof(string), required: true, description: "Comma delimited list of DataHub permissions")]
[Option("description", typeof(string), required: false, description: "Description of the role")]
public class RegisterRoleCommand : SubCommand<RegisterCommand>
{
    public RegisterRoleCommand(IServiceProvider serviceProvider) : base("role", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string name, string permissions, string? description = null, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<RegisterRoleResponse>(new SerializedRequest
        {
            RequestType = nameof(RegisterRoleRequest),
            Data = JsonConvert.SerializeObject(new RegisterRoleRequest
            {
                Name = name,
                Description = description,
                Permissions = ParsePermissions(permissions)
            })
        }, cancellationToken);

        if (!response.Success)
        {
            AnsiConsole.WriteLine("Failed to register role: " + response.FailureReason);
            return 0;
        }

        AnsiConsole.WriteLine("Register role succeeded");
        return 1;
    }

    private static List<string> ParsePermissions(string permissions)
    {
        return permissions?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(permission => permission.Trim().ToLowerInvariant())
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }
}
