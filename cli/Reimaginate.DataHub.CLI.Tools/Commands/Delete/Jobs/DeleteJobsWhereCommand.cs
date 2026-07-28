using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Delete.Jobs;

[Argument("where", required: true)]
public class DeleteJobsWhereCommand : SubCommand<DeleteJobsCommand>
{
    public DeleteJobsWhereCommand(IServiceProvider serviceProvider) : base("where", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }
    
    public async Task<int> HandleCommand(string where, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<DeleteJobsResponse>(new SerializedRequest()
        {
            RequestType = nameof(DeleteJobsRequest),
            Data = JsonConvert.SerializeObject(new DeleteJobsRequest()
            {
                Where = where
            })
        }, cancellationToken);

        if (!response.Success)
        {
            AnsiConsole.WriteLine("Delete jobs failed: " + response.FailureReason);
            return 0;
        }
        
        AnsiConsole.WriteLine("Delete successful");
        return 1;
    }
}

