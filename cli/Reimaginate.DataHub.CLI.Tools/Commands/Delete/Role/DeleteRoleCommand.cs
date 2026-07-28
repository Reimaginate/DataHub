using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Delete.Role;

[Argument("name", typeof(string), required: true, description: "Name of the role")]
public class DeleteRoleCommand : SubCommand<DeleteCommand>
{
    public DeleteRoleCommand(IServiceProvider serviceProvider) : base("role", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string name, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<DeleteRoleResponse>(new SerializedRequest
        {
            RequestType = nameof(DeleteRoleRequest),
            Data = JsonConvert.SerializeObject(new DeleteRoleRequest
            {
                Name = name
            })
        }, cancellationToken);

        if (!response.Success)
        {
            AnsiConsole.WriteLine("Delete role failed: " + response.FailureReason);
            return 0;
        }

        AnsiConsole.WriteLine("Delete role succeeded");
        return 1;
    }
}
