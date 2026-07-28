using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Locks;

public class GetLocksCommand : SubCommand<GetCommand>
{
    public GetLocksCommand(IServiceProvider serviceProvider) : base("locks", "List processing locks", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var response = await adminApi.PostAdminMessage<RetrieveProcessingLocksResponse>(new SerializedRequest
        {
            RequestType = nameof(RetrieveProcessingLocksRequest),
            Data = JsonConvert.SerializeObject(new RetrieveProcessingLocksRequest())
        }, cancellationToken);

        ConsoleHelper.PrintTable(response.ProcessingLocks ?? [], []);
        AnsiConsole.WriteLine();
        return 1;
    }
}
