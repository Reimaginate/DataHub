using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
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

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(RebaseEntityTrackingResult.DataSource),
        nameof(RebaseEntityTrackingResult.EntityType),
        nameof(RebaseEntityTrackingResult.EntityId),
        nameof(RebaseEntityTrackingResult.Success),
        nameof(RebaseEntityTrackingResult.FailureReason),
    });

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
        var failures = new List<object>();

        var idsToProcess = new List<string>(entityIds);

        while (idsToProcess.Any())
        {
            if (cancelled) break;

            var batch = idsToProcess.Take(5000).ToList();

            switch (dataSource.ToLower())
            {
                case "datahub":
                    var rebaseDataHubEntitiesRequest = new SerializedRequest()
                    {
                        RequestType = nameof(RebaseDataHubEntitiesRequest),
                        Data = JsonConvert.SerializeObject(new RebaseDataHubEntitiesRequest()
                        {
                            EntityType = entityType,
                            EntityIds = batch
                        })
                    };

                    var rebaseDataHubEntitiesResponse = await adminApi.PostAdminMessage<RebaseDataHubEntitiesResponse>(rebaseDataHubEntitiesRequest, cancellationToken);
                    failures.AddRange(rebaseDataHubEntitiesResponse.Results.Where(w => !w.Success));
                    break;

                default:
                    var rebaseSourceEntitiesRequest = new SerializedRequest()
                    {
                        RequestType = nameof(RebaseSourceEntitiesRequest),
                        Data = JsonConvert.SerializeObject(new RebaseSourceEntitiesRequest()
                        {
                            DataSource = dataSource,
                            EntityType = entityType,
                            EntityIds = batch
                        })
                    };

                    var rebaseSourceEntitiesResponse = await adminApi.PostAdminMessage<RebaseSourceEntitiesResponse>(rebaseSourceEntitiesRequest, cancellationToken);
                    failures.AddRange(rebaseSourceEntitiesResponse.Results.Where(w => !w.Success));
                    break;
            }

            idsToProcess.RemoveRange(0, batch.Count);

            if (!Console.IsOutputRedirected)
            {
                Console.WriteLine($@"{idsToProcess.Count} Remaining");
            }

            if (!Console.IsInputRedirected)
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

        if (failures.Any())
        {
            return 0;
        }

        return 1;
    }
}

