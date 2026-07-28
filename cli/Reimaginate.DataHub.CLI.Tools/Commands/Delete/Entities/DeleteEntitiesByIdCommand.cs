using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Commands.Delete.Jobs;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;
using DeleteDataHubEntitiesRequest = Reimaginate.DataHub.SharedModels.Requests.Client.DeleteDataHubEntitiesRequest;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Delete.Entities;

[Argument("entityType", required: true)]
[Argument("ids", allowMultiple: true, required: false, type: typeof(string[]))]
[Option("from-file", typeof(string), required: false, aliases: "f")]
public class DeleteEntitiesByIdCommand : SubCommand<DeleteEntitiesCommand>
{
    public DeleteEntitiesByIdCommand(IServiceProvider serviceProvider) : base("byid", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string entityType, string[] ids, string? fromFile, CancellationToken cancellationToken = default)
        => await HandleCommandWithOptions(entityType, ids, fromFile, dryRun: false, yes: false, cancellationToken);

    public async Task<int> HandleCommandWithOptions(string entityType, string[] ids, string? fromFile, bool dryRun, bool yes, CancellationToken cancellationToken = default)
    {
        if ((ids == null || !ids.Any()) && string.IsNullOrEmpty(fromFile))
        {
            AnsiConsole.WriteLine("No ids provided");
            return 0;
        }

        var entityIds = ids?.ToList() ?? [];

        if (!string.IsNullOrEmpty(fromFile))
        {
            if (!File.Exists(fromFile))
            {
                AnsiConsole.WriteLine("The specified file does not exist.");
                return 1;
            }

            var fileContent = await File.ReadAllLinesAsync(fromFile, cancellationToken);
            foreach (var line in fileContent)
            {
                if (line.Contains(" "))
                {
                    var lineIds = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                    entityIds.AddRange(lineIds);
                    continue;
                }   

                entityIds.Add(line.Trim());
            }
        }

        if (dryRun)
        {
            ConsoleHelper.PrintTable(entityIds.Select(id => new DeleteEntityPreview { EntityType = entityType, EntityId = id }).ToList(), [nameof(DeleteEntityPreview.EntityType), nameof(DeleteEntityPreview.EntityId)]);
            return 1;
        }

        if (!yes && !AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE?[/]"))
        {
            AnsiConsole.WriteLine();
            return CliExitCodes.Cancelled;
        }

        
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<DeleteDataHubEntitiesResponse>(new SerializedRequest()
        {
            RequestType = nameof(DeleteDataHubEntitiesRequest),
            Data = JsonConvert.SerializeObject(new DeleteDataHubEntitiesRequest()
            {
                EntityType = entityType,
                EntityIds = entityIds
            })
        }, cancellationToken);

        if (!response.Success)
        {
            AnsiConsole.WriteLine("Delete entities failed: " + string.Join("\n", response.Failures.Select(s => $"{s.DataHubEntity}: {s.FailureReason}")));
            return 0;
        }

        AnsiConsole.WriteLine("Delete successful");
        return 1;
    }

    private sealed class DeleteEntityPreview
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
    }
}
