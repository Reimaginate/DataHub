using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Submit.Job;

[Argument("file-path", required: true, description: "Path to json job definition")]
public class SubmitJobCommand : SubCommand<SubmitCommand>
{
    public SubmitJobCommand(IServiceProvider serviceProvider) : base("job", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string filepath, CancellationToken cancellationToken)
    {

        var fileContent = await File.ReadAllTextAsync(filepath, cancellationToken);
        var jobRequest = JObject.Parse(fileContent);

        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var submitJobRequest = jobRequest[nameof(SubmitJobRequest.Request)] != null
            ? jobRequest.ToObject<SubmitJobRequest>()!
            : new SubmitJobRequest
            {
                Type = jobRequest.Value<string>("RequestType") ?? jobRequest.Value<string>("Type") ?? "JobRequest",
                Name = jobRequest.Value<string>("Name") ?? Path.GetFileNameWithoutExtension(filepath),
                Target = jobRequest.Value<string>("Target") ?? "DataMaintenanceAgent",
                Request = jobRequest
            };

        submitJobRequest.RequestType = nameof(SubmitJobRequest);

        var response = await adminApi.PostAdminMessage<SubmitJobResponse>(new SerializedRequest()
        {
            RequestType = nameof(SubmitJobRequest),
            Data = JsonConvert.SerializeObject(submitJobRequest)
        }, cancellationToken);

        ConsoleHelper.PrintTable([response], [$"{nameof(SubmitJobResponse.Result)}.{nameof(Reimaginate.DataHub.SharedModels.Core.Models.DTO.JobDTO.JobId)}", nameof(SubmitJobResponse.Success), nameof(SubmitJobResponse.FailureReason)]);

        return response.Success ? 1 : 0;
    }
}
