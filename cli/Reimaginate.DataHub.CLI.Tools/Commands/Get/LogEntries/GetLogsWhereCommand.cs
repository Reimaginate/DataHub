using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Logs;

[Argument("where", required: false)]
[Option("additional-properties", required: false, aliases: "add-props")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]
public class GetLogsWhereCommand : SubCommand<GetLogsCommand>
{
    public GetLogsWhereCommand(IServiceProvider serviceProvider) : base("where", "Query log entries", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new[]
    {
        nameof(LogEntry.id),
        nameof(LogEntry.Type),
        nameof(LogEntry.Timestamp)
    });

    public async Task<int> HandleCommand(string? where, string? properties = null, string? additionalProperties = null, bool dontOpen = false, bool info = false, int? pageSize = 100, string saveTo = "", CancellationToken cancellationToken = default)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var request = new GetLogEntriesWhereRequest
        {
            WhereClause = where,
            Select = string.Join(",", props.Select(prop => $"x.{prop}")),
            PageSize = pageSize ?? 100,
            GetTotalResultCount = info
        };

        if (info)
        {
            var infoResponse = await adminApi.PostAdminMessage<GetLogEntriesResponse>(new SerializedRequest
            {
                RequestType = nameof(GetLogEntriesWhereRequest),
                Data = JsonConvert.SerializeObject(request)
            }, cancellationToken);

            ConsoleHelper.PrintTable([new LogEntryInfo { Count = infoResponse.ResultCount }], [nameof(LogEntryInfo.Count)]);
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(saveTo))
        {
            var outcome = await CliHelpers.SaveToAsync(saveTo, () =>
                CliHelpers.RetrieveAllResultsAsync<GetLogEntriesWhereRequest, GetLogEntriesResponse, LogEntry>(
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

        var response = await adminApi.PostAdminMessage<GetLogEntriesResponse>(new SerializedRequest
        {
            RequestType = nameof(GetLogEntriesWhereRequest),
            Data = JsonConvert.SerializeObject(request)
        }, cancellationToken);

        ConsoleHelper.PrintTable(response.Results ?? [], props);
        AnsiConsole.WriteLine();
        return 1;
    }

    private sealed class LogEntryInfo
    {
        public int Count { get; set; }
    }
}
