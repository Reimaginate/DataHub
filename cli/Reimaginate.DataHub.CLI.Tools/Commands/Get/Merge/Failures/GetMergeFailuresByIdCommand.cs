using System.CommandLine.NamingConventionBinder;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Merge.Failures;

[Argument("ids", required: true, allowMultiple: true, type: typeof(string[]))]
[Option("additional-properties", required: false, aliases: "add-props,ap")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]
public class GetMergeFailuresByIdCommand : SubCommand<GetMergeFailuresCommand>
{
    public GetMergeFailuresByIdCommand(IServiceProvider serviceProvider) : base("byid", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(MergeFailureDTO.Id),
        nameof(MergeFailureDTO.Timestamp),
        nameof(MergeFailureDTO.DataHubEntityType),
        nameof(MergeFailureDTO.DataHubEntityId),
        nameof(MergeFailureDTO.Description),
    });

    public async Task<int> HandleCommand(string[] ids, string? properties = null, string? additionalProperties = null, bool info = false, string saveTo = "", bool dontOpen = false)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);

        var baseReq = new GetMergeFailuresByIdRequest()
        {
            Ids = ids.ToList()
        };

        var resultsFunc = () => CliHelpers.RetrieveAllResultsAsync<GetMergeFailuresByIdRequest, GetMergeFailuresResponse, MergeFailureDTO>(ServiceProvider, baseReq, r => r.Results, r => r.MoreResultsAvailable);

        var outcome = await CliHelpers.SaveToAsync(saveTo, resultsFunc, props, dontOpen)
                      ?? await CliHelpers.ProcessInfo(info, resultsFunc, _defaultProperties);

        if (outcome != null) return outcome.Value;

        var results = await resultsFunc();

        ConsoleHelper.PrintTable(results, props);
        AnsiConsole.WriteLine();

        return 1;
    }
}
