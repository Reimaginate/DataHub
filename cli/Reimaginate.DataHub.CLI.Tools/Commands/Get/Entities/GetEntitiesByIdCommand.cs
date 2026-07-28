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

[Argument("entityType", required: true)]
[Argument("ids", allowMultiple: true, required: true, type: typeof(string[]))]
[Option("additional-properties", required: false, aliases: "add-props,ap")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
[Option("expand-results", typeof(bool), required: false, aliases: "expand")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]
public class GetEntitiesByIdCommand : SubCommand<GetEntitiesCommand>
{
    public GetEntitiesByIdCommand(IServiceProvider serviceProvider) : base("byid", serviceProvider)
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

    public async Task<int> HandleCommand(string entityType, string[] ids, string? properties = null, string? additionalProperties = null, int? pageSize = 100, bool info = false, string saveTo = "", bool dontOpen = false, bool expandResults = false, CancellationToken cancellationToken = default)
    {
        if (ids.Any(s => s.Contains(" ")))
        {
            ids = ids.SelectMany(s => s.Split(" ")).ToArray();
        }

        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);

        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var idsToRetrieve = new List<string>(ids);
        var batch = idsToRetrieve.Take(pageSize.GetValueOrDefault(100)).ToList();

        var response = await adminApi.PostAdminMessage<GetEntitiesResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetEntitiesByIdRequest),
            Data = JsonConvert.SerializeObject(new GetEntitiesByIdRequest()
            {
                EntityType = entityType,
                EntityIds = batch
            })
        }, cancellationToken);

        idsToRetrieve.RemoveRange(0, batch.Count);
        
        var results = CliHelpers.SummarizeResults(expandResults, response.Results);
       
        if (!Console.IsInputRedirected)
        {
            ConsoleHelper.PrintTable(results, props);
            AnsiConsole.WriteLine();
        }

        while (idsToRetrieve.Any())
        {
            if (!Console.IsInputRedirected)
            {
                var nextPage = AnsiConsole.Confirm("There are more results available - would you like to continue?");
                if (!nextPage) break;
            }

            batch = idsToRetrieve.Take(pageSize.GetValueOrDefault(100)).ToList();

            response = await adminApi.PostAdminMessage<GetEntitiesResponse>(new SerializedRequest()
            {
                RequestType = nameof(GetEntitiesByIdRequest),
                Data = JsonConvert.SerializeObject(new GetEntitiesByIdRequest()
                {
                    EntityType = entityType,
                    EntityIds = batch
                })
            }, cancellationToken);

            results = CliHelpers.SummarizeResults(expandResults, response.Results);
          
            if (!Console.IsInputRedirected)
            {
                ConsoleHelper.PrintTable(results, props);
                AnsiConsole.WriteLine();
            }

            idsToRetrieve.RemoveRange(0, batch.Count);
        }

        return 1;
    }
}
