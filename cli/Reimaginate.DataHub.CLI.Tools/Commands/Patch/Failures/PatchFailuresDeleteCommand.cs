using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Patch.Failures;

[Argument("where", required: false)]
public class PatchFailuresDeleteCommand : SubCommand<PatchFailuresCommand>
{
    private const int DeletePageSize = 500;

    public PatchFailuresDeleteCommand(IServiceProvider serviceProvider) : base("delete", "Delete patch failures", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string? where = null, CancellationToken cancellationToken = default)
    {
        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        var cliApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var request = new GetPatchFailuresWhereRequest
        {
            Select = "x.id",
            WhereClause = where,
            PageSize = DeletePageSize,
            GetTotalResultCount = true
        };

        var deleteFailures = new List<DeleteLogFailure>();
        var matchedCount = 0;
        var deletedCount = 0;

        await CliProgressHelper.RunAsync(async ctx =>
        {
            var response = await CliHelpers.RetrievePagedResultsAsync<GetPatchFailuresWhereRequest, GetPatchFailuresResponse>(ServiceProvider, request);
            if (!response.Results.Any())
            {
                return;
            }

            var deleteTask = ctx.AddTask($"Deleting {response.ResultCount} patch failures", Math.Max(response.ResultCount, 1));
            deleteTask.StartTask();

            while (response.Results.Any())
            {
                var pageIds = response.Results.Select(failure => failure.Id).ToList();
                matchedCount += pageIds.Count;

                var deleteResponse = await cliApi.PostAdminMessage<DeletePatchFailuresResponse>(new SerializedRequest
                {
                    RequestType = nameof(DeletePatchFailuresRequest),
                    Data = JsonConvert.SerializeObject(new DeletePatchFailuresRequest
                    {
                        PatchFailureIds = pageIds
                    })
                }, cancellationToken);

                var pageFailures = deleteResponse.DeleteFailures ?? new List<DeleteLogFailure>();
                if (!deleteResponse.Success)
                {
                    deleteFailures.AddRange(pageFailures);
                }

                var pageDeletedCount = deleteResponse.Success ? pageIds.Count : Math.Max(pageIds.Count - pageFailures.Count, 0);
                deletedCount += pageDeletedCount;
                deleteTask.Increment(pageDeletedCount);

                if (!response.MoreResultsAvailable || pageDeletedCount == 0 || pageFailures.Any())
                {
                    break;
                }

                request.ContinuationToken = null;
                request.GetTotalResultCount = false;
                response = await CliHelpers.RetrievePagedResultsAsync<GetPatchFailuresWhereRequest, GetPatchFailuresResponse>(ServiceProvider, request);
            }

            deleteTask.StopTask();
        });

        if (matchedCount == 0)
        {
            AnsiConsole.WriteLine("No results");
            return 1;
        }

        AnsiConsole.WriteLine($"Deleted {deletedCount} of {matchedCount} matched patch failures");
        BulkOperationDetailsPrompt.Show(
            deleteFailures,
            [
                nameof(DeleteLogFailure.LogEntryId),
                nameof(DeleteLogFailure.FailureReason)
            ],
            "patch-failures-delete-errors",
            "failed patch failure delete result(s)");
        return deleteFailures.Any() ? 0 : 1;
    }
}
