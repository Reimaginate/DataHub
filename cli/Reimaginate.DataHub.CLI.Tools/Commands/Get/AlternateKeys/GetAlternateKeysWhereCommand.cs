using System.CommandLine.NamingConventionBinder;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.AlternateKeys;

[Argument("where", required: true)]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]
public class GetAlternateKeysWhereCommand : SubCommand<GetAlternateKeysCommand>
{
    public GetAlternateKeysWhereCommand(IServiceProvider serviceProvider) : base("where", serviceProvider)
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

    public async Task<int> HandleCommand(string where, string? properties = null, int? pageSize = 500, bool info = false, string saveTo = "", bool dontOpen = false, CancellationToken cancellationToken = default)
    {

        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties);

        var whereReq = new GetAlternateKeysWhereRequest()
        {
            WhereClause = where,
            PageSize = pageSize ?? 5000
        };

        if (!string.IsNullOrEmpty(saveTo))
        {
            return await CliHelpers.SaveToAsync(whereReq, saveTo, !dontOpen, ServiceProvider);
        }


        var alternateKeysResponse = await CliHelpers.RetrievePagedResultsAsync<GetAlternateKeysWhereRequest, GetAlternateKeysResponse>(ServiceProvider, whereReq);
       
        ConsoleHelper.PrintTable(alternateKeysResponse.Results, props);
        AnsiConsole.WriteLine();

        while (alternateKeysResponse.MoreResultsAvailable)
        {
            var nextPage = AnsiConsole.Confirm("There are more results available - would you like to continue?");
            if (!nextPage) break;

            whereReq.ContinuationToken = alternateKeysResponse.ContinuationToken;
            alternateKeysResponse = await CliHelpers.RetrievePagedResultsAsync<GetAlternateKeysWhereRequest, GetAlternateKeysResponse>(ServiceProvider, whereReq);

            
            ConsoleHelper.PrintTable(alternateKeysResponse.Results, props);
            AnsiConsole.WriteLine();
        }


        return 1;

    }
}
