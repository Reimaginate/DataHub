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

[Argument("where", required: false)]
[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("save-to", required: false, aliases: "s")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
public class GetAlertsWhereCommand : SubCommand<GetAlertsCommand>
{
    public GetAlertsWhereCommand(IServiceProvider serviceProvider) : base("where", "List alerts by query", serviceProvider)
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

    public async Task<int> HandleCommand(string? where, string? properties = null, string? additionalProperties = null, int? pageSize = 100, bool info = false, string saveTo = "", bool dontOpen = false, CancellationToken cancellationToken = default)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var request = new GetAlertsWhereRequest
        {
            WhereClause = where,
            PageSize = pageSize ?? 100,
            GetTotalResultCount = info
        };

        if (info)
        {
            var infoResponse = await adminApi.PostAdminMessage<GetAlertsResponse>(new SerializedRequest
            {
                RequestType = nameof(GetAlertsWhereRequest),
                Data = JsonConvert.SerializeObject(request)
            }, cancellationToken);

            ConsoleHelper.PrintTable([new AlertInfo { Count = infoResponse.ResultCount }], [nameof(AlertInfo.Count)]);
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(saveTo))
        {
            var outcome = await CliHelpers.SaveToAsync(saveTo, () =>
                CliHelpers.RetrieveAllResultsAsync<GetAlertsWhereRequest, GetAlertsResponse, AlertDTO>(
                    ServiceProvider,
                    request,
                    response => response.Results ?? [],
                    response => response.MoreResultsAvailable,
                    (req, res) => req.ContinuationToken = res.ContinuationToken),
                props,
                dontOpen);

            if (outcome.HasValue)
            {
                return outcome.Value;
            }
        }

        var response = await adminApi.PostAdminMessage<GetAlertsResponse>(new SerializedRequest
        {
            RequestType = nameof(GetAlertsWhereRequest),
            Data = JsonConvert.SerializeObject(request)
        }, cancellationToken);

        ConsoleHelper.PrintTable(response.Results ?? [], props);
        AnsiConsole.WriteLine();
        return 1;
    }

    private sealed class AlertInfo
    {
        public int Count { get; set; }
    }
}
