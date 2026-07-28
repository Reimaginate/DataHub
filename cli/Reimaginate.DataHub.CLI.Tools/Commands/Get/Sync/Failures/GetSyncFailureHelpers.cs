using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Commands.Sync;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Sync.Failures;

public static class GetSyncFailureHelpers
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
                var getSyncFailuresResponse = await adminApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetSyncFailuresWhereRequest),
                    Data = JsonConvert.SerializeObject(new GetSyncFailuresWhereRequest()
                    {
                        GetTotalResultCount = true,
                        WhereClause = where,
                        PageSize = 1000
                    })
                });

                var totalCount = getSyncFailuresResponse.ResultCount;

                var rebaseTask = ctx.AddTask($"Processing {totalCount} records");
                rebaseTask.StartTask();

                var groupedByType = getSyncFailuresResponse.Results.Where(w => w.DataSource != null && w.SourceEntityType != null).GroupBy(g => new { g.DataSource, g.SourceEntityType });
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


                while (getSyncFailuresResponse.MoreResultsAvailable)
                {
                    getSyncFailuresResponse = await adminApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                    {
                        RequestType = nameof(GetSyncFailuresWhereRequest),
                        Data = JsonConvert.SerializeObject(new GetSyncFailuresWhereRequest()
                        {
                            GetTotalResultCount = false,
                            ContinuationToken = getSyncFailuresResponse.ContinuationToken,
                            WhereClause = where,
                            PageSize = 1000
                        })
                    });

                    groupedByType = getSyncFailuresResponse.Results.Where(w => w.DataSource != null && w.SourceEntityType != null).GroupBy(g => new { g.DataSource, g.SourceEntityType });
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

                var getSyncFailuresResponse = await adminApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetSyncFailuresWhereRequest),
                    Data = JsonConvert.SerializeObject(new GetSyncFailuresWhereRequest()
                    {
                        GetTotalResultCount = true,
                        WhereClause = where,
                        PageSize = 1000
                    })
                });

                var totalCount = getSyncFailuresResponse.ResultCount;

                var rebaseTask = ctx.AddTask($"Processing {totalCount} records");
                rebaseTask.StartTask();


                var groupedByType = getSyncFailuresResponse.Results.Where(w => w.DataHubEntityType != null).GroupBy(g => g.DataHubEntityType);
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

                var batchPercent = Convert.ToDouble(getSyncFailuresResponse.Results.Count) / Convert.ToDouble(totalCount) * 100;
                rebaseTask.Increment(batchPercent);


                while (getSyncFailuresResponse.MoreResultsAvailable)
                {
                    getSyncFailuresResponse = await adminApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                    {
                        RequestType = nameof(GetSyncFailuresWhereRequest),
                        Data = JsonConvert.SerializeObject(new GetSyncFailuresWhereRequest()
                        {
                            GetTotalResultCount = false,
                            ContinuationToken = getSyncFailuresResponse.ContinuationToken,
                            WhereClause = where,
                            PageSize = 1000
                        })
                    });

                    groupedByType = getSyncFailuresResponse.Results.Where(w => w.DataHubEntityType != null).GroupBy(g => g.DataHubEntityType);
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

                    batchPercent = Convert.ToDouble(getSyncFailuresResponse.Results.Count) / Convert.ToDouble(totalCount) * 100;
                    rebaseTask.Increment(batchPercent);
                }

                rebaseTask.Value = 100;
                rebaseTask.StopTask();
            });
    }

    public static async Task<int> Retry(ICLIApi adminApi, string? where, CancellationToken cancellationToken, IServiceProvider serviceProvider)
    {
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        var commandResult = 1;

        await CliProgressHelper
            .RunAsync(async ctx =>
            {
                AnsiConsole.WriteLine("Retrying sync failures (press esc to cancel)");

                var getSyncFailuresResponse = await adminApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetSyncFailuresWhereRequest),
                    Data = JsonConvert.SerializeObject(new GetSyncFailuresWhereRequest()
                    {
                        GetTotalResultCount = true,
                        WhereClause = where,
                        PageSize = 1000
                    })
                }, cancellationToken);

                var totalCount = getSyncFailuresResponse.ResultCount;

                var rebaseTask = ctx.AddTask($"Processing {totalCount} records");
                rebaseTask.StartTask();
                
                var groupedByType = getSyncFailuresResponse.Results.Where(w => w.DataHubEntityType != null).GroupBy(g => new { g.DataSource, g.DataHubEntityType });
                foreach (var entityType in groupedByType)
                {
                    var entityIds = entityType.Select(s => s.DataHubEntityId).ToList();
                    var syncCommand = serviceProvider.GetRequiredService<SyncCommand>();
                    var result = await syncCommand.HandleCommand(entityType.Key.DataSource, entityType.Key.DataHubEntityType, entityIds.ToArray(), CancellationToken.None);
                    if (result != 1)
                    {
                        commandResult = 0;
                    }
                }

                var batchPercent = Convert.ToDouble(getSyncFailuresResponse.Results.Count) / Convert.ToDouble(totalCount) * 100;
                rebaseTask.Increment(batchPercent);
                
                while (getSyncFailuresResponse.MoreResultsAvailable)
                {
                    getSyncFailuresResponse = await adminApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                    {
                        RequestType = nameof(GetSyncFailuresWhereRequest),
                        Data = JsonConvert.SerializeObject(new GetSyncFailuresWhereRequest()
                        {
                            GetTotalResultCount = false,
                            ContinuationToken = getSyncFailuresResponse.ContinuationToken,
                            WhereClause = where,
                            PageSize = 1000
                        })
                    }, cancellationToken);

                    groupedByType = getSyncFailuresResponse.Results.Where(w => w.DataHubEntityType != null).GroupBy(g => new { g.DataSource, g.DataHubEntityType });
                    foreach (var entityType in groupedByType)
                    {
                        var entityIds = entityType.Select(s => s.DataHubEntityId).ToList();
                        var syncCommand = serviceProvider.GetRequiredService<SyncCommand>();
                        var result = await syncCommand.HandleCommand(entityType.Key.DataSource, entityType.Key.DataHubEntityType, entityIds.ToArray(), CancellationToken.None);
                        if (result != 1)
                        {
                            commandResult = 0;
                        }
                    }

                    batchPercent = Convert.ToDouble(getSyncFailuresResponse.Results.Count) / Convert.ToDouble(totalCount) * 100;
                    rebaseTask.Increment(batchPercent);
                }

                rebaseTask.Value = 100;
                rebaseTask.StopTask();
            });

        return commandResult;
    }


}
