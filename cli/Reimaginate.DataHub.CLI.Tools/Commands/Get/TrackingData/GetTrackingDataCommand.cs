using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.TrackingData;

[Argument("dataSource", required: true)]
[Argument("entityType", required: true)]
[Argument("entityId", required: true)]
[Option("additional-properties", required: false, aliases: "add-props,ap")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("properties", required: false, aliases: "props,p")]

public class GetTrackingDataCommand : SubCommand<GetCommand>
{
    public GetTrackingDataCommand(IServiceProvider serviceProvider) : base("trackingdata", "Retrieve change tracking entries from the Data Hub", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(ChangeTrackingEntry.id),
        nameof(ChangeTrackingEntry.DataSource),
        nameof(ChangeTrackingEntry.EntityType),
        nameof(ChangeTrackingEntry.EntityId),
        nameof(ChangeTrackingEntry.EntryType),
        nameof(ChangeTrackingEntry.Timestamp)
    });

    public async Task<int> HandleCommand(string dataSource, string entityType, string entityId, string? properties = null, string? additionalProperties = null, int? pageSize = 100, bool info = false)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);

        var baseReq = new GetTrackingDataRequest()
        {
            DataSource = dataSource,
            EntityType = entityType,
            EntityIds = entityId == "*" ? null : new List<string>() { entityId },
            PageSize = 1000
        };

        var resultsFunc = () => CliHelpers.RetrieveAllResultsAsync<GetTrackingDataRequest, GetTrackingDataResponse, ChangeTrackingEntry>(ServiceProvider, baseReq, r => r.Results, r => r.MoreResultsAvailable, (req, res) => req.ContinuationToken = res.ContinuationToken);

        var outcome = await CliHelpers.ProcessInfo(info, () => resultsFunc(), _defaultProperties);

        if (outcome != null) return outcome.Value;

        baseReq.PageSize = pageSize;

        var cliApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await cliApi.PostAdminMessage<GetTrackingDataResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetTrackingDataRequest),
            Data = JsonConvert.SerializeObject(baseReq)
        });

        ConsoleHelper.PrintTable(response.Results, props);
        AnsiConsole.WriteLine();

        while (response.MoreResultsAvailable)
        {
            var nextPage = AnsiConsole.Confirm("There are more results available - would you like to continue?");
            if (!nextPage) break;

            try
            {
                baseReq.ContinuationToken = response.ContinuationToken;
                response = await cliApi.PostAdminMessage<GetTrackingDataResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetTrackingDataRequest),
                    Data = JsonConvert.SerializeObject(baseReq)
                });
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(new Exception($"Could not connect to Data Hub: {ex.Message}", ex));
                return 0;
            }

            ConsoleHelper.PrintTable(response.Results, props);
            AnsiConsole.WriteLine();
        }

        return 1;
    }
}
