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
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Patch.Failures;

[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props,ap")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("save-to", required: false, aliases: "s")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]

public class GetPatchFailuresCommand : SubCommand<GetPatchCommand>
{
    public GetPatchFailuresCommand(IServiceProvider serviceProvider) : base("failures", "Retrieve patch failures from the Data Hub", serviceProvider)
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

    public async Task<int> HandleCommand(string? properties = null, string? additionalProperties = null, int? pageSize = 100, bool info = false, string saveTo = "", bool dontOpen = false,  CancellationToken cancellationToken = default)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);

        var baseReq = new GetPatchFailuresWhereRequest();

        var resultsFunc = () => CliHelpers.RetrieveAllResultsAsync<GetPatchFailuresWhereRequest, GetPatchFailuresResponse, PatchFailureDTO>(ServiceProvider, baseReq, r => r.Results, r => r.MoreResultsAvailable, (req, res) => req.ContinuationToken = res.ContinuationToken);
        
        var outcome = await CliHelpers.SaveToAsync(saveTo, resultsFunc, props, dontOpen)
                      ?? await CliHelpers.ProcessInfo(info, resultsFunc, _defaultProperties);

        if (outcome != null) return outcome.Value;

        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
      
        var response = await adminApi.PostAdminMessage<GetPatchFailuresResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetPatchFailuresWhereRequest),
            Data = JsonConvert.SerializeObject(new GetPatchFailuresWhereRequest()
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
                response = await adminApi.PostAdminMessage<GetPatchFailuresResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetPatchFailuresWhereRequest),
                    Data = JsonConvert.SerializeObject(new GetPatchFailuresWhereRequest()
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
