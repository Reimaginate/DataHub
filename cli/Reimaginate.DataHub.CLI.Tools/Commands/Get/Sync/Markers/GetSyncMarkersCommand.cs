using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Sync.Markers;

[Option("additional-properties", required: false, aliases: "add-props,ap")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
public class GetSyncMarkersCommand : SubCommand<GetSyncCommand>
{
    public GetSyncMarkersCommand(IServiceProvider serviceProvider) : base("markers", "Retrieve sync markers from the Data Hub", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(SyncMarker.id),
        nameof(SyncMarker.DataSource),
        nameof(SyncMarker.AgentId),
        nameof(SyncMarker.EntityType),
        nameof(SyncMarker.Value)
    });

    public async Task<int> HandleCommand(string? properties = null, string? addProps = null, bool info = false, string saveTo = "", bool dontOpen = false)
    {
        GetSyncMarkersResponse response;

        try
        {
            var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

            response = await adminApi.PostAdminMessage<GetSyncMarkersResponse>(new SerializedRequest()
            {
                RequestType = nameof(GetSyncMarkersRequest),
                Data = JsonConvert.SerializeObject(new GetSyncMarkersRequest())
            });
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(new Exception("Could not connect to Data Hub", ex));
            return 0;
        }

        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, addProps);

        var outcome = await CliHelpers.ProcessInfo(info, () => Task.FromResult(response.Results), _defaultProperties);

        if (outcome != null) return outcome.Value;

        var results = response.Results.OrderBy(o => o.DataSource).ThenBy(o => o.AgentId).ThenBy(o => o.EntityType).ToList();
        outcome = await CliHelpers.SaveToAsync(saveTo, () => Task.FromResult(results), props, dontOpen);

        if (outcome != null) return outcome.Value;

        ConsoleHelper.PrintTable(results, props);
        AnsiConsole.WriteLine();
        return 1;
    }
}
