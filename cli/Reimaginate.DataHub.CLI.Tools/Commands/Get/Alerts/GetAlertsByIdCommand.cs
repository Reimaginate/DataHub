using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Alerts;

[Argument("ids", required: true, allowMultiple: true, type: typeof(string[]))]
[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props")]
public class GetAlertsByIdCommand : SubCommand<GetAlertsCommand>
{
    public GetAlertsByIdCommand(IServiceProvider serviceProvider) : base("byid", "Get alerts by id", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new[]
    {
        nameof(AlertDTO.Id),
        nameof(AlertDTO.Timestamp),
        nameof(AlertDTO.Severity),
        nameof(AlertDTO.Subject),
        nameof(AlertDTO.Description)
    });

    public async Task<int> HandleCommand(string[] ids, string? properties = null, string? additionalProperties = null, CancellationToken cancellationToken = default)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var response = await adminApi.PostAdminMessage<GetAlertsResponse>(new SerializedRequest
        {
            RequestType = nameof(GetAlertsByIdRequest),
            Data = JsonConvert.SerializeObject(new GetAlertsByIdRequest { Ids = ids.ToList() })
        }, cancellationToken);

        ConsoleHelper.PrintTable(response.Results ?? [], props);
        AnsiConsole.WriteLine();
        return 1;
    }
}
