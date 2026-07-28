using System.CommandLine.NamingConventionBinder;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.AlternateKeys;

[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
public class GetAlternateKeysCommand : SubCommand<GetCommand>
{
    public GetAlternateKeysCommand(IServiceProvider serviceProvider) : base("alternatekeys", "Retrieve entity alternate keys from the Data Hub", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(DataHubEntityAlternateKey.EntityId),
        nameof(DataHubEntityAlternateKey.EntityType),
        nameof(DataHubEntityAlternateKey.Key),
        nameof(DataHubEntityAlternateKey.Value)
    });


    public async Task<int> HandleCommand(string? properties = null, int? pageSize = 100, bool info = false, string saveTo = "", bool dontOpen = false)
    {
        if (info)
        {
            var entityCounts = await CliHelpers.RetrieveEntityInfoAsync(ServiceProvider);
            ConsoleHelper.PrintTable(entityCounts, new List<string>() { nameof(EntityTypeCount.EntityType), nameof(EntityTypeCount.Count) });
            return 1;
        }

        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties);

        var whereReq = new GetAlternateKeysWhereRequest()
        {
            PageSize = pageSize ?? 5000
        };

        if (!string.IsNullOrEmpty(saveTo))
        {
            await CliHelpers.SaveToAsync(whereReq, saveTo, !dontOpen, ServiceProvider);
            return 1;
        }

        var getEntitiesResponse = await CliHelpers.RetrievePagedResultsAsync<GetAlternateKeysWhereRequest, GetAlternateKeysResponse>(ServiceProvider, whereReq);
        ConsoleHelper.PrintTable(getEntitiesResponse.Results, props);
        AnsiConsole.WriteLine();

        while (getEntitiesResponse.MoreResultsAvailable)
        {
            var nextPage = AnsiConsole.Confirm("There are more results available - would you like to continue?");
            if (!nextPage) break;

            whereReq.ContinuationToken = getEntitiesResponse.ContinuationToken;
            getEntitiesResponse = await CliHelpers.RetrievePagedResultsAsync<GetAlternateKeysWhereRequest, GetAlternateKeysResponse>(ServiceProvider, whereReq);
            
            ConsoleHelper.PrintTable(getEntitiesResponse.Results, props);
            AnsiConsole.WriteLine();
        }


        return 1;
    }
}
