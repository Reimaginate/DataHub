using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;
using System.CommandLine.NamingConventionBinder;

using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Jobs;

[Argument("where", required: true)]
[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("save-to", required: false, aliases: "s")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
public class GetJobsWhereCommand : SubCommand<GetJobsCommand>
{
    public GetJobsWhereCommand(IServiceProvider serviceProvider) : base("where", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(JobDTO.JobId),
        $"{nameof(JobDTO.CreatedBy)}.{nameof(JobDTO.CreatedBy.Name)}",
        nameof(JobDTO.CompletedOn),
        $"{nameof(JobDTO.Request)}.RequestType",
        nameof(JobDTO.Status)
    });

    public async Task<int> HandleCommand(string where, string? properties = null, string? additionalProperties = null, int? pageSize = 100, bool info = false, string saveTo = "", bool dontOpen = false, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);
        var request = new GetJobsRequest
        {
            Where = where,
            PageSize = pageSize ?? 100,
            GetTotalResultCount = info
        };

        if (info)
        {
            var infoResponse = await adminApi.PostAdminMessage<GetJobsResponse>(new SerializedRequest
            {
                RequestType = nameof(GetJobsRequest),
                Data = JsonConvert.SerializeObject(request)
            }, cancellationToken);

            ConsoleHelper.PrintTable([new JobInfo { Count = infoResponse.ResultCount }], [nameof(JobInfo.Count)]);
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(saveTo))
        {
            var outcome = await CliHelpers.SaveToAsync(saveTo, async () =>
                CliHelpers.SummarizeResults(true, await CliHelpers.RetrieveAllResultsAsync<GetJobsRequest, GetJobsResponse, JobDTO>(
                    ServiceProvider,
                    request,
                    response => response.Results ?? [],
                    response => response.MoreResultsAvailable,
                    (req, res) => req.ContinuationToken = res.ContinuationToken)),
                props,
                dontOpen);

            if (outcome.HasValue)
            {
                return outcome.Value;
            }
        }

        var response = await adminApi.PostAdminMessage<GetJobsResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetJobsRequest),
            Data = JsonConvert.SerializeObject(request)
        }, cancellationToken);

        var jobs = response.Results;

        if (jobs == null || !jobs.Any())
        {
            AnsiConsole.WriteLine("No results");
            return 1;
        }

        var results = CliHelpers.SummarizeResults(true, jobs);

        ConsoleHelper.PrintTable(results, props);
        AnsiConsole.WriteLine();

        return 1;
    }

    private sealed class JobInfo
    {
        public int Count { get; set; }
    }
}
