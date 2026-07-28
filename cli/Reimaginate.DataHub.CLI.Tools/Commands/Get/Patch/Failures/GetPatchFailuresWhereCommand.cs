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

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Patch.Failures;

[Argument("where", required: true)]
[Option("additional-properties", required: false, aliases: "add-props,ap")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
public class GetPatchFailuresWhereCommand : SubCommand<GetPatchFailuresCommand>
{
    public GetPatchFailuresWhereCommand(IServiceProvider serviceProvider) : base("where", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(PatchFailureDTO.Id),
        nameof(PatchFailureDTO.Timestamp),
        nameof(PatchFailureDTO.EventSource),
        nameof(PatchFailureDTO.DataSource),
        nameof(PatchFailureDTO.EntityType),
        nameof(PatchFailureDTO.EntityId),
        nameof(PatchFailureDTO.FailureReason),
    });


    public async Task<int> HandleCommand(string where, string? additionalProperties = null, bool dontOpen = false, bool info = false, int? pageSize = 100, string? properties = null, string saveTo = "", CancellationToken cancellationToken = default)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);

        var baseReq = new GetPatchFailuresWhereRequest()
        {
            WhereClause = where,
            PageSize = 1000
        };

        void AdditionalSummary(List<PatchFailureDTO> results, List<object> summaryTable)
        {
            summaryTable.Add(new { Property = "", Value = "" });
            summaryTable.Add(new { Property = "--------------", Value = "------" });
            summaryTable.Add(new { Property = "Failure Types", Value = "Count" });
            summaryTable.Add(new { Property = "--------------", Value = "------" });

            var failureTypeGroups = results.GroupBy(g => g.FailureReason);
            foreach (var failureTypeGroup in failureTypeGroups)
            {
                var groupResultCount = failureTypeGroup.Count();
                summaryTable.Add(new { Property = failureTypeGroup.Key, Value = groupResultCount });
            }
        }

        var resultsFunc = () => CliHelpers.RetrieveAllResultsAsync<GetPatchFailuresWhereRequest, GetPatchFailuresResponse, PatchFailureDTO>(ServiceProvider, baseReq, r => r.Results, r => r.MoreResultsAvailable, (req, res) => req.ContinuationToken = res.ContinuationToken);

        var outcome = await CliHelpers.SaveToAsync(saveTo, resultsFunc, props, dontOpen)
                      ?? await CliHelpers.ProcessInfo(info, resultsFunc, _defaultProperties, AdditionalSummary);


        if (outcome != null) return outcome.Value;

        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        
        var response = await adminApi.PostAdminMessage<GetPatchFailuresResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetPatchFailuresWhereRequest),
            Data = JsonConvert.SerializeObject(new GetPatchFailuresWhereRequest()
            {
                WhereClause = where,
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
                response = await adminApi.PostAdminMessage<GetPatchFailuresResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetPatchFailuresWhereRequest),
                    Data = JsonConvert.SerializeObject(new GetPatchFailuresWhereRequest()
                    {
                        WhereClause = where,
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
