using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Rebase;

[Argument("dataSource", required: true)]
[Argument("entityType", required: true)]
[Argument("entityIds", allowMultiple: true, required: true, type: typeof(string[]))]
public class RebaseCommand : TopLevelCommand
{
    private readonly IServiceProvider _serviceProvider;

    public RebaseCommand(IServiceProvider serviceProvider) : base("rebase", serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string dataSource, string entityType, string[] entityIds, CancellationToken cancellationToken)
    {
        //AnsiConsole.ResetColors();
        //AnsiConsole.WriteLine();

        //AnsiConsole.MarkupLine("[white]Press ESC to cancel[/]");

        entityIds = entityIds.SelectMany(s =>
        {
            if (s.Contains(" "))
            {
                return s.Split(" ").ToArray();
            }
            return new[] { s };
        }).ToArray();

        if (!Console.IsOutputRedirected)
        {
            var totalCount = entityIds.Length;
            Console.WriteLine($@"{totalCount} Remaining");
        }

        var adminApi = _serviceProvider.GetRequiredService<ICLIApi>();

        var cancelled = false;
        var failures = new List<RebaseEntityTrackingResult>();
        var succeeded = 0;
        long archivedCount = 0;

        var idsToProcess = new List<string>(entityIds);

        try
        {
            while (idsToProcess.Any())
            {
                if (cancelled || cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                var batch = idsToProcess.Take(5000).ToList();

                switch (dataSource.ToLower())
                {
                    case "datahub":
                        var rebaseDataHubEntitiesRequest = new SerializedRequest()
                        {
                            RequestType = nameof(RebaseDataHubEntitiesRequest),
                            Data = JsonConvert.SerializeObject(new RebaseDataHubEntitiesRequest()
                            {
                                IncludeArchivedTrackingEntries = false,
                                EntityType = entityType,
                                EntityIds = batch
                            })
                        };

                        var rebaseDataHubEntitiesResponse = await adminApi.PostAdminMessage<RebaseDataHubEntitiesResponse>(rebaseDataHubEntitiesRequest, cancellationToken);
                        CliHelpers.ShowRebaseWarnings(rebaseDataHubEntitiesResponse.Results);
                        failures.AddRange(rebaseDataHubEntitiesResponse.Results.Where(w => !w.Success));
                        succeeded += rebaseDataHubEntitiesResponse.Results.Count(w => w.Success);
                        archivedCount += rebaseDataHubEntitiesResponse.Results.Sum(result => (long)result.ArchivedTrackingEntryCount);
                        break;

                    default:
                        var rebaseSourceEntitiesRequest = new SerializedRequest()
                        {
                            RequestType = nameof(RebaseSourceEntitiesRequest),
                            Data = JsonConvert.SerializeObject(new RebaseSourceEntitiesRequest()
                            {
                                IncludeArchivedTrackingEntries = false,
                                DataSource = dataSource,
                                EntityType = entityType,
                                EntityIds = batch
                            })
                        };

                        var rebaseSourceEntitiesResponse = await adminApi.PostAdminMessage<RebaseSourceEntitiesResponse>(rebaseSourceEntitiesRequest, cancellationToken);
                        CliHelpers.ShowRebaseWarnings(rebaseSourceEntitiesResponse.Results);
                        failures.AddRange(rebaseSourceEntitiesResponse.Results.Where(w => !w.Success));
                        succeeded += rebaseSourceEntitiesResponse.Results.Count(w => w.Success);
                        archivedCount += rebaseSourceEntitiesResponse.Results.Sum(result => (long)result.ArchivedTrackingEntryCount);
                        break;
                }

                idsToProcess.RemoveRange(0, batch.Count);

                if (!Console.IsOutputRedirected)
                {
                    Console.WriteLine($@"{idsToProcess.Count} Remaining");
                }

                if (idsToProcess.Count > 0 && !Console.IsInputRedirected)
                {
                    while (Console.KeyAvailable)
                    {
                        var kp = Console.ReadKey();
                        if (kp.Key == ConsoleKey.Escape)
                        {
                            cancelled = true;
                            break;
                        }
                    }
                }
            }

        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
        }

        Console.Error.WriteLine($"Rebase finished: {succeeded} succeeded, {failures.Count} failed, {idsToProcess.Count} unconfirmed. Archived {archivedCount} tracking entries.");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"{failure.DataSource}/{failure.EntityType}/{failure.EntityId}: {failure.FailureReason ?? "Rebase failed."} Confirmed cutoff: {failure.RebasedTo?.ToString("O") ?? "none"}.");
        }

        if (cancelled)
        {
            Console.Error.WriteLine("Rebase cancelled.");
            return CliExitCodes.Cancelled;
        }

        return failures.Count > 0 ? CliExitCodes.Failure : CliExitCodes.Success;
    }
}
