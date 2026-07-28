using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Entities;

[Argument("where", required: false)]
public class GetEntityCountsCommand : SubCommand<GetEntitiesCommand>
{
    public GetEntityCountsCommand(IServiceProvider serviceProvider) : base("counts", "Count entities by entity type", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string? where = null, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var response = await adminApi.PostAdminMessage<GetDataHubEntityTypeCountsResponse>(new SerializedRequest
        {
            RequestType = nameof(GetDataHubEntityTypeCountsRequest),
            Data = JsonConvert.SerializeObject(new GetDataHubEntityTypeCountsRequest { WhereClause = where })
        }, cancellationToken);

        ConsoleHelper.PrintTable(response.Results, [nameof(EntityTypeCount.EntityType), nameof(EntityTypeCount.Count)]);
        AnsiConsole.WriteLine();
        return 1;
    }
}
