using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Merge.Failures;

[Option("additional-properties", required: false, aliases: "add-props,ap")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]

public class GetMergeFailuresCommand : SubCommand<GetMergeCommand>
{
    public GetMergeFailuresCommand(IServiceProvider serviceProvider) : base("failures", "Retrieve sync failures from the Data Hub", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(MergeFailureDTO.Id),
        nameof(MergeFailureDTO.Timestamp),
        nameof(MergeFailureDTO.DataSource),
        nameof(MergeFailureDTO.SourceEntityType),
        nameof(MergeFailureDTO.SourceEntityId),
        nameof(MergeFailureDTO.FailureType),
        nameof(MergeFailureDTO.Description),
    });

    public async Task<int> HandleCommand(string? properties = null, string? additionalProperties = null, int? pageSize = 100, bool info = false, string saveTo = "", bool dontOpen = false, CancellationToken cancellationToken = default)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);

        var baseReq = new GetMergeFailuresWhereRequest();

        var resultsFunc = () => CliHelpers.RetrieveAllResultsAsync<GetMergeFailuresWhereRequest, GetMergeFailuresResponse, MergeFailureDTO>(ServiceProvider, baseReq, r => r.Results, r => r.MoreResultsAvailable, (req, res) => req.ContinuationToken = res.ContinuationToken);

        var additionalSummary = (List<MergeFailureDTO> results, List<object> summaryTable) =>
        {
            summaryTable.Add(new { Property = "", Value = "" });
            summaryTable.Add(new { Property = "--------------", Value = "------" });
            summaryTable.Add(new { Property = "Failure Types", Value = "Count" });
            summaryTable.Add(new { Property = "--------------", Value = "------" });

            var failureTypeGroups = results.GroupBy(g => g.FailureType);
            foreach (var failureTypeGroup in failureTypeGroups)
            {
                var groupResultCount = failureTypeGroup.Count();
                summaryTable.Add(new { Property = failureTypeGroup.Key, Value = groupResultCount });
            }
        };

        var outcome = await CliHelpers.SaveToAsync(saveTo, resultsFunc, props, dontOpen)
                      ?? await CliHelpers.ProcessInfo(info, resultsFunc, _defaultProperties, additionalSummary);

        if (outcome != null) return outcome.Value;

        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        
        var response = await adminApi.PostAdminMessage<GetMergeFailuresResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetMergeFailuresWhereRequest),
            Data = JsonConvert.SerializeObject(new GetMergeFailuresWhereRequest()
            {
                PageSize = pageSize ?? 100
            })
        }, cancellationToken);

        ConsoleHelper.PrintTable(response.Results, props);
        AnsiConsole.WriteLine();

        while (response.MoreResultsAvailable)
        {
            var nextPage = AnsiConsole.Confirm("There are more results available - would you like to continue?");
            if (!nextPage) break;

            try
            {
                response = await adminApi.PostAdminMessage<GetMergeFailuresResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetMergeFailuresWhereRequest),
                    Data = JsonConvert.SerializeObject(new GetMergeFailuresWhereRequest()
                    {
                        ContinuationToken = response.ContinuationToken,
                        PageSize = pageSize ?? 100
                    })
                }, cancellationToken);
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
