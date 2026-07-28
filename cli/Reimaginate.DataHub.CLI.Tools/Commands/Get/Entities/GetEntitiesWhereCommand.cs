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

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Entities;

[Argument("where", required: true)]
[Option("additional-properties", required: false, aliases: "add-props")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
[Option("expand-results", typeof(bool), required: false, aliases: "expand")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]

public class GetEntitiesWhereCommand : SubCommand<GetEntitiesCommand>
{
    public GetEntitiesWhereCommand(IServiceProvider serviceProvider) : base("where", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);

    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(DataHubEntity.id),
        nameof(DataHubEntity.entityType),
        nameof(DataHubEntity.lastUpdated),
        nameof(DataHubEntity.alternateKeys)
    });

    public async Task<int> HandleCommand(string where, string? properties = null, string? additionalProperties = null, int? pageSize = 500, bool info = false, string saveTo = "", bool dontOpen = false, bool expandResults = false, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        if (info)
        {
            var entityCounts = await CliHelpers.RetrieveEntityInfoAsync(ServiceProvider, where);
            ConsoleHelper.PrintTable(entityCounts, [nameof(EntityTypeCount.EntityType), nameof(EntityTypeCount.Count)]);
            return 0;
        }

        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);

        var whereReq = new GetEntitiesWhereRequest()
        {
            Select = string.Join(",", props.Select(prop => $"x.{prop}")),
            WhereClause = where,
            PageSize = pageSize ?? 5000
        };

        if (!string.IsNullOrEmpty(saveTo))
        {
            return await CliHelpers.SaveToAsync(whereReq, saveTo, !dontOpen, ServiceProvider);
        }

        var response = await adminApi.PostAdminMessage<GetEntitiesResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetEntitiesWhereRequest),
            Data = JsonConvert.SerializeObject(new GetEntitiesWhereRequest()
            {
                WhereClause = where,
                Select = string.Join(",", props.Select(prop =>
                {
                    var p = prop;
                    if (p.Contains("["))
                    {
                        p = p.Split("[")[0];
                    }
                    return $"x.{p}";
                })),
                PageSize = pageSize ?? 50
            })
        }, cancellationToken);
        
        var results = CliHelpers.SummarizeResults(expandResults, response.Results);
        ConsoleHelper.PrintTable(results, props);
        AnsiConsole.WriteLine();

        while (response.MoreResultsAvailable)
        {
            var nextPage = AnsiConsole.Confirm("There are more results available - would you like to continue?");
            if (!nextPage) break;

            response = await adminApi.PostAdminMessage<GetEntitiesResponse>(new SerializedRequest()
            {
                RequestType = nameof(GetEntitiesWhereRequest),
                Data = JsonConvert.SerializeObject(new GetEntitiesWhereRequest()
                {
                    WhereClause = where,
                    Select = string.Join(",", props.Select(prop => $"x.{prop}")),
                    PageSize = pageSize ?? 50,
                    ContinuationToken = response.ContinuationToken
                })
            }, cancellationToken);


            results = CliHelpers.SummarizeResults(expandResults, response.Results);
            ConsoleHelper.PrintTable(results, props);
            AnsiConsole.WriteLine();
        }
        
        return 1;
    }
}
