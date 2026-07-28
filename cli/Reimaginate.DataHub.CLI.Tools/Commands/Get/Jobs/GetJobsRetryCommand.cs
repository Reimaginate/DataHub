using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Jobs;

[Argument("jobId", required: true)]
public class GetJobsRetryCommand : SubCommand<GetJobsCommand>
{
    public GetJobsRetryCommand(IServiceProvider serviceProvider) : base("retry", "Retry a job", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string jobId, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var response = await adminApi.PostAdminMessage<RetryJobResponse>(new SerializedRequest
        {
            RequestType = nameof(RetryJobRequest),
            Data = JsonConvert.SerializeObject(new RetryJobRequest { JobId = jobId })
        }, cancellationToken);

        AnsiConsole.WriteLine(response.Success ? "Retry job succeeded" : response.FailureReason ?? "Retry job failed");
        return response.Success ? 1 : 0;
    }
}
