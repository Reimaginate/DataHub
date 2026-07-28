using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Jobs;

[Argument("jobId", required: true)]
[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props")]
public class GetJobsByIdCommand : SubCommand<GetJobsCommand>
{
    public GetJobsByIdCommand(IServiceProvider serviceProvider) : base("byid", "Get a job by id", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new[]
    {
        nameof(JobDTO.JobId),
        nameof(JobDTO.Type),
        nameof(JobDTO.Name),
        nameof(JobDTO.Status),
        nameof(JobDTO.CreatedOn),
        nameof(JobDTO.CompletedOn)
    });

    public async Task<int> HandleCommand(string jobId, string? properties = null, string? additionalProperties = null, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var response = await adminApi.PostAdminMessage<GetJobResponse>(new SerializedRequest
        {
            RequestType = nameof(GetJobRequest),
            Data = JsonConvert.SerializeObject(new GetJobRequest { JobId = jobId })
        }, cancellationToken);

        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);
        ConsoleHelper.PrintTable(new[] { response.Result }.Where(job => job != null).ToList(), props);
        AnsiConsole.WriteLine();
        return 1;
    }
}
