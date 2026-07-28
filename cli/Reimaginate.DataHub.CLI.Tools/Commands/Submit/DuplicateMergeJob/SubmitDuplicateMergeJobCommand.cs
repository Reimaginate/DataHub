using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs.JobRequests;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Submit.DuplicateMergeJob;

[Argument("entityType", required: true, description: "The DataHub entity type")]
[Argument("entityIds", allowMultiple: true, required: true, type: typeof(string[]))]
[Option("surviving-entity-id", required: false, description: "Id of the surviving entity")]
[Option("automerge", typeof(bool), required: false, description: "Auto merge the entities")]
[Option("data-file-path", required: false, description: "File path to the surviving entity data")]
public class SubmitDuplicateMergeJobCommand : SubCommand<SubmitCommand>
{
    public SubmitDuplicateMergeJobCommand(IServiceProvider serviceProvider) : base("duplicatemergejob", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(
        string entityType,
        string[] entityIds,
        string survivingEntityId,
        bool automerge,
        string dataFilePath,
        CancellationToken cancellationToken)
    {
        var mergePlan = new DuplicateMergePlan
        {
            EntityType = entityType,
            EntityIds = entityIds.ToList(),
            AutoMerge = automerge || string.IsNullOrEmpty(survivingEntityId),
            SurvivingEntityId = survivingEntityId,
            SurvivingEntityData = await ReadOptionalDataFile(dataFilePath, cancellationToken)
        };

        var duplicateMergeRequest = new DuplicateMergeRequest
        {
            MergePlan = mergePlan
        };

        var submitJobRequest = new SubmitJobRequest
        {
            Type = nameof(DuplicateMergeRequest),
            Name = $"{nameof(DuplicateMergeRequest)}: {entityType}",
            Target = "DataMaintenanceAgent",
            Request = JObject.FromObject(duplicateMergeRequest),
            RequestType = nameof(SubmitJobRequest)
        };

        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var response = await adminApi.PostAdminMessage<SubmitJobResponse>(new SerializedRequest
        {
            RequestType = nameof(SubmitJobRequest),
            Data = JsonConvert.SerializeObject(submitJobRequest)
        }, cancellationToken);

        ConsoleHelper.PrintTable(
            [response],
            [$"{nameof(SubmitJobResponse.Result)}.{nameof(Reimaginate.DataHub.SharedModels.Core.Models.DTO.JobDTO.JobId)}", nameof(SubmitJobResponse.Success), nameof(SubmitJobResponse.FailureReason)]);

        return response.Success ? 1 : 0;
    }

    private static async Task<JToken?> ReadOptionalDataFile(string dataFilePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dataFilePath))
        {
            return null;
        }

        var fileContent = await File.ReadAllTextAsync(dataFilePath, cancellationToken);
        return JToken.Parse(fileContent);
    }
}
