using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Enable.User;

[Argument("id", required: true, type: typeof(string))]
public class EnableUserCommand : SubCommand<EnableCommand>
{
    public EnableUserCommand(IServiceProvider serviceProvider) : base("user", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string id, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<EnableUserResponse>(new SerializedRequest()
        {
            RequestType = nameof(EnableUserRequest),
            Data = JsonConvert.SerializeObject(new EnableUserRequest()
            {
               Id = id
            })
        }, cancellationToken);

        if (!response.Success)
        {
            AnsiConsole.WriteLine("Enable user failed: " + response.FailureReason);
            return 0;
        }

        AnsiConsole.WriteLine("Enable user successful");
        return 1;
    }
}

