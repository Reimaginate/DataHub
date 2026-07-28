using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Delete.Jobs;

[Argument("ids", allowMultiple: true, required: true, type: typeof(string[]))]
public class DeleteJobsByIdCommand : SubCommand<DeleteJobsCommand>
{
    public DeleteJobsByIdCommand(IServiceProvider serviceProvider) : base("byid", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string[] ids, CancellationToken cancellationToken = default)
        => await HandleCommandWithOptions(ids, dryRun: false, yes: true, cancellationToken);

    public async Task<int> HandleCommandWithOptions(string[] ids, bool dryRun, bool yes, CancellationToken cancellationToken = default)
    {
        if (ids.Length == 0)
        {
            AnsiConsole.WriteLine("No job ids provided");
            return 0;
        }

        if (dryRun)
        {
            ConsoleHelper.PrintTable(ids.Select(id => new DeleteJobPreview { JobId = id }).ToList(), [nameof(DeleteJobPreview.JobId)]);
            return 1;
        }

        if (!yes && !AnsiConsole.Confirm($"[bold red]WARNING: This will delete {ids.Length} job(s). Are you sure you want to continue?[/]"))
        {
            AnsiConsole.WriteLine();
            return CliExitCodes.Cancelled;
        }

        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var parameters = ids
            .Select((id, index) => new DataHubQueryParameter { Name = $"id{index}", Value = id })
            .ToList();

        var response = await adminApi.PostAdminMessage<DeleteJobsResponse>(new SerializedRequest()
        {
            RequestType = nameof(DeleteJobsRequest),
            Data = JsonConvert.SerializeObject(new DeleteJobsRequest()
            {
                Where = $"x.id in ({string.Join(",", parameters.Select(parameter => $"@{parameter.Name}"))})",
                Parameters = parameters
            })
        }, cancellationToken);

        if (!response.Success)
        {
            AnsiConsole.WriteLine("Delete jobs failed: " + response.FailureReason);
            return 0;
        }

        AnsiConsole.WriteLine("Delete successful");
        return 1;
    }

    private sealed class DeleteJobPreview
    {
        public string JobId { get; set; } = string.Empty;
    }
}
