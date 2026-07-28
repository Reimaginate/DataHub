using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Spectre.Console;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.CLI.Tools.Commands.Merge;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Merge.Failures;

public static class GetMergeFailureHelpers
{
    public static async Task RebaseSourceEntities(ICLIApi adminApi, string where, IMediator mediator, CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return;
        }

        AnsiConsole.Markup("[yellow]Deleting sync failures (press esc to cancel)[/]");

        await CliProgressHelper
            .RunAsync(async ctx =>
            {
                var getMergeFailuresResponse = await adminApi.PostAdminMessage<GetMergeFailuresResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetMergeFailuresWhereRequest),
                    Data = JsonConvert.SerializeObject(new GetMergeFailuresWhereRequest()
                    {
                        GetTotalResultCount = true,
                        WhereClause = where,
                        PageSize = 1000
                    })
                });

                var totalCount = getMergeFailuresResponse.ResultCount;

                var rebaseTask = ctx.AddTask($"Processing {totalCount} records");
                rebaseTask.StartTask();

                var groupedByType = getMergeFailuresResponse.Results.Where(w => w.DataSource != null && w.SourceEntityType != null).GroupBy(g => new { g.DataSource, g.SourceEntityType });
                foreach (var entityType in groupedByType)
                {
                    var entitiesToRebase = entityType.Select(s => s.SourceEntityId).ToList();
                    if (entitiesToRebase.Any())
                    {
                        _ = (await mediator.TrySend<SendResponse>(new SendRequest(new RebaseSourceEntitiesRequest()
                        {
                            DataSource = entityType.Key.DataSource,
                            EntityType = entityType.Key.SourceEntityType,
                            EntityIds = entitiesToRebase
                        }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

                        var batchPercent = Convert.ToDouble(entitiesToRebase.Count) / Convert.ToDouble(totalCount) * 100;
                        rebaseTask.Increment(batchPercent);
                    }
                }


                while (getMergeFailuresResponse.MoreResultsAvailable)
                {
                    getMergeFailuresResponse = await adminApi.PostAdminMessage<GetMergeFailuresResponse>(new SerializedRequest()
                    {
                        RequestType = nameof(GetMergeFailuresWhereRequest),
                        Data = JsonConvert.SerializeObject(new GetMergeFailuresWhereRequest()
                        {
                            GetTotalResultCount = false,
                            ContinuationToken = getMergeFailuresResponse.ContinuationToken,
                            WhereClause = where,
                            PageSize = 1000
                        })
                    });

                    groupedByType = getMergeFailuresResponse.Results.Where(w => w.DataSource != null && w.SourceEntityType != null).GroupBy(g => new { g.DataSource, g.SourceEntityType });
                    foreach (var entityType in groupedByType)
                    {
                        var entitiesToRebase = entityType.Select(s => s.SourceEntityId).ToList();
                        if (entitiesToRebase.Any())
                        {
                            _ = (await mediator.TrySend<SendResponse>(new SendRequest(new RebaseSourceEntitiesRequest()
                            {
                                DataSource = entityType.Key.DataSource,
                                EntityType = entityType.Key.SourceEntityType,
                                EntityIds = entitiesToRebase
                            }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

                            var batchPercent = Convert.ToDouble(entitiesToRebase.Count) / Convert.ToDouble(totalCount) * 100;
                            rebaseTask.Increment(batchPercent);
                        }
                    }
                }

                rebaseTask.Value = 100;
                rebaseTask.StopTask();

            });
    }

    public static async Task RebaseDataHubEntities(ICLIApi adminApi, string where, CancellationToken cancellationToken, IMediator mediator)
    {
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return;
        }

        await CliProgressHelper
            .RunAsync(async ctx =>
            {
                AnsiConsole.WriteLine("Rebasing DataHub entities (press esc to cancel)");

                var getMergeFailuresResponse = await adminApi.PostAdminMessage<GetMergeFailuresResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetMergeFailuresWhereRequest),
                    Data = JsonConvert.SerializeObject(new GetMergeFailuresWhereRequest()
                    {
                        GetTotalResultCount = true,
                        WhereClause = where,
                        PageSize = 1000
                    })
                });

                var totalCount = getMergeFailuresResponse.ResultCount;

                var rebaseTask = ctx.AddTask($"Processing {totalCount} records");
                rebaseTask.StartTask();


                var groupedByType = getMergeFailuresResponse.Results.Where(w => w.DataHubEntityType != null).GroupBy(g => g.DataHubEntityType);
                foreach (var entityType in groupedByType)
                {
                    var entitiesToRebase = entityType.Select(s => s.DataHubEntityId).ToList();
                    if (entitiesToRebase.Any())
                    {
                        _ = (await mediator.TrySend<SendResponse>(new SendRequest(new RebaseDataHubEntitiesRequest()
                        {
                            EntityType = entityType.Key,
                            EntityIds = entitiesToRebase
                        }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };
                    }
                }

                var batchPercent = Convert.ToDouble(getMergeFailuresResponse.Results.Count) / Convert.ToDouble(totalCount) * 100;
                rebaseTask.Increment(batchPercent);


                while (getMergeFailuresResponse.MoreResultsAvailable)
                {
                    getMergeFailuresResponse = await adminApi.PostAdminMessage<GetMergeFailuresResponse>(new SerializedRequest()
                    {
                        RequestType = nameof(GetMergeFailuresWhereRequest),
                        Data = JsonConvert.SerializeObject(new GetMergeFailuresWhereRequest()
                        {
                            GetTotalResultCount = false,
                            ContinuationToken = getMergeFailuresResponse.ContinuationToken,
                            WhereClause = where,
                            PageSize = 1000
                        })
                    });

                    groupedByType = getMergeFailuresResponse.Results.Where(w => w.DataHubEntityType != null).GroupBy(g => g.DataHubEntityType);
                    foreach (var entityType in groupedByType)
                    {
                        var entitiesToRebase = entityType.Select(s => s.DataHubEntityId).ToList();
                        if (entitiesToRebase.Any())
                        {
                            _ = (await mediator.TrySend<SendResponse>(new SendRequest(new RebaseDataHubEntitiesRequest()
                            {
                                EntityType = entityType.Key,
                                EntityIds = entitiesToRebase
                            }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };


                        }
                    }

                    batchPercent = Convert.ToDouble(getMergeFailuresResponse.Results.Count) / Convert.ToDouble(totalCount) * 100;
                    rebaseTask.Increment(batchPercent);
                }

                rebaseTask.Value = 100;
                rebaseTask.StopTask();
            });
    }

    public static async Task<int?> RetryMergeFailures(IServiceProvider serviceProvider, Func<Task<List<MergeFailure>>> mergeFailuresFunc)
    {
        var result = 1;

        var results = await mergeFailuresFunc();

        var entityTypeGroups = results.GroupBy(g => new { g.DataSource, g.DataHubEntityType, g.SourceEntityType });
        foreach (var entityTypeGroup in entityTypeGroups)
        {
            var entityIds = entityTypeGroup.Select(s => s.SourceEntityId).ToList();
            var mergeCommand = serviceProvider.GetRequiredService<MergeCommand>();
            var retryResult = await mergeCommand.HandleCommand(entityTypeGroup.Key.DataSource, entityTypeGroup.Key.DataHubEntityType, entityIds.ToArray(), CancellationToken.None);
            if (retryResult != 1)
            {
                result = 0;
            }
        }
        return result;
    }
}
