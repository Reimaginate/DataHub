using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Sync.Failures;

[Argument("where", required: true)]
[Option("additional-properties", required: false, aliases: "add-props,ap")]
[Option("dont-open", typeof(bool), required: false, aliases: "no")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("page-size", typeof(int), required: false, aliases: "page")]
[Option("properties", required: false, aliases: "props,p")]
[Option("save-to", required: false, aliases: "s")]
public class GetSyncFailuresWhereCommand : SubCommand<GetSyncFailuresCommand>
{
    public GetSyncFailuresWhereCommand(IServiceProvider serviceProvider) : base("where", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(SyncFailureDTO.Id),
        nameof(SyncFailureDTO.Timestamp),
        nameof(SyncFailureDTO.DataHubEntityType),
        nameof(SyncFailureDTO.DataHubEntityId),
        nameof(SyncFailureDTO.FailureType),
        nameof(SyncFailureDTO.Description),
    });


    public async Task<int> HandleCommand(string where, string? additionalProperties = null, bool dontOpen = false, bool info = false, int? pageSize = 100, string? properties = null, string saveTo = "", CancellationToken cancellationToken = default)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);

        var whereReq = new GetSyncFailuresWhereRequest()
        {
            WhereClause = where,
            PageSize = pageSize ?? 500
        };

        var resultsFunc = () => CliHelpers.RetrieveAllResultsAsync<GetSyncFailuresWhereRequest, GetSyncFailuresResponse, SyncFailureDTO>(ServiceProvider, whereReq, r => r.Results, r => r.MoreResultsAvailable, (req, res) => req.ContinuationToken = res.ContinuationToken);

        var additionalSummary = (List<SyncFailureDTO> results, List<object> summaryTable) =>
        {
            summaryTable.Add(new { Property = "", Value = "" });
            summaryTable.Add(new { Property = "--------------", Value = "------" });
            summaryTable.Add(new { Property = "Failure Types", Value = "Count" });
            summaryTable.Add(new { Property = "--------------", Value = "------" });

            var failureTypeGroups = results.GroupBy(g => g.FailureType);
            foreach (var failureTypeGroup in failureTypeGroups)
            {
                var groupResultCount = failureTypeGroup.Count();
                summaryTable.Add(new { Property = failureTypeGroup.Key, Value = groupResultCount });
            }
        };

        var outcome = await CliHelpers.SaveToAsync(saveTo, resultsFunc, props, dontOpen)
                      ?? await CliHelpers.ProcessInfo(info, resultsFunc, _defaultProperties, additionalSummary);

        if (outcome != null) return outcome.Value;

        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetSyncFailuresWhereRequest),
            Data = JsonConvert.SerializeObject(new GetSyncFailuresWhereRequest()
            {
                WhereClause = where,
                PageSize = pageSize ?? 100
            })
        }, cancellationToken);

        if (Console.IsOutputRedirected)
        {
            foreach (var result in response.Results)
            {
                var cols = new List<string>();
                props.ForEach(e =>
                {
                    var val = ConsoleHelper.GetCellValue(result, e);
                    if (val.Contains(" ") || val.Contains('\t')) val = $"\"{val}\"";
                    cols.Add(val);
                });

                var outputLine = string.Join('\t', cols);
                Console.WriteLine(outputLine);
            }

            try
            {
                response = await adminApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                {
                    RequestType = nameof(GetSyncFailuresWhereRequest),
                    Data = JsonConvert.SerializeObject(new GetSyncFailuresWhereRequest()
                    {
                        WhereClause = where,
                        ContinuationToken = response.ContinuationToken,
                        PageSize = pageSize ?? 100
                    })
                }, cancellationToken);

                foreach (var result in response.Results)
                {
                    var cols = new List<string>();
                    props.ForEach(e =>
                    {
                        var val = ConsoleHelper.GetCellValue(result, e);
                        if (val.Contains(" ") || val.Contains('\t')) val = $"\"{val}\"";
                        cols.Add(val);
                    });

                    var outputLine = string.Join('\t', cols);
                    Console.WriteLine(outputLine);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(new Exception($"Could not connect to Data Hub: {ex.Message}", ex));
                return 0;
            }

            return 1;
        }

        if (!Console.IsOutputRedirected)
        {
            ConsoleHelper.PrintTable(response.Results, props);
            AnsiConsole.WriteLine();

            while (response.MoreResultsAvailable)
            {
                var nextPage = AnsiConsole.Confirm("There are more results available - would you like to continue?");
                if (!nextPage) break;

                try
                {
                    response = await adminApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                    {
                        RequestType = nameof(GetSyncFailuresWhereRequest),
                        Data = JsonConvert.SerializeObject(new GetSyncFailuresWhereRequest()
                        {
                            WhereClause = where,
                            ContinuationToken = response.ContinuationToken,
                            PageSize = pageSize ?? 100
                        })
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteException(new Exception($"Could not connect to Data Hub: {ex.Message}", ex));
                    return 0;
                }

                ConsoleHelper.PrintTable(response.Results, props);
                AnsiConsole.WriteLine();
            }
        }

        return 1;
    }

    private static async Task DetachEntities(ICLIApi adminApi, IMediator mediator, string where, string dataSource, CancellationToken cancellationToken)
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
                AnsiConsole.WriteLine("Detaching entities (press esc to cancel)");

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

                var detachTask = ctx.AddTask($"Processing {totalCount} records");
                detachTask.StartTask();


                var groupedByType = getSyncFailuresResponse.Results.Where(w => w.DataHubEntityType != null).GroupBy(g => g.DataHubEntityType);
                foreach (var entityType in groupedByType)
                {
                    var entitiesToDetach = entityType.Select(s => s.DataHubEntityId).ToList();
                    if (entitiesToDetach.Any())
                    {
                        _ = (await mediator.TrySend<SendResponse>(new SendRequest(new DetachEntitiesRequest()
                        {
                            EntityType = entityType.Key,
                            EntityIds = entitiesToDetach,
                            DataSource = dataSource

                        }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };
                    }
                }

                var batchPercent = Convert.ToDouble(getSyncFailuresResponse.Results.Count) / Convert.ToDouble(totalCount) * 100;
                detachTask.Increment(batchPercent);


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
                        var entitiesToDetach = entityType.Select(s => s.DataHubEntityId).ToList();
                        if (entitiesToDetach.Any())
                        {
                            _ = (await mediator.TrySend<SendResponse>(new SendRequest(new DetachEntitiesRequest()
                            {
                                EntityType = entityType.Key,
                                EntityIds = entitiesToDetach,
                                DataSource = dataSource

                            }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };
                        }
                    }

                    batchPercent = Convert.ToDouble(getSyncFailuresResponse.Results.Count) / Convert.ToDouble(totalCount) * 100;
                    detachTask.Increment(batchPercent);
                }

                detachTask.Value = 100;
                detachTask.StopTask();
            });
    }

    private static async Task RebaseSourceEntities(ICLIApi adminApi, IMediator mediator, string where, string rebaseTo, CancellationToken cancellationToken)
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
                            EntityIds = entitiesToRebase,
                            RebaseTo = rebaseTo?.ToLower() == "now" ? DateTimeOffset.Now : DateTimeOffset.Parse(rebaseTo!)
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
                                EntityIds = entitiesToRebase,
                                RebaseTo = rebaseTo?.ToLower() == "now" ? DateTimeOffset.Now : DateTimeOffset.Parse(rebaseTo!)
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

    private static async Task RebaseDataHubEntities(ICLIApi adminApi, IMediator mediator, string where, string rebaseTo, CancellationToken cancellationToken)
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
                            EntityIds = entitiesToRebase,
                            RebaseTo = rebaseTo?.ToLower() == "now" ? DateTimeOffset.Now : DateTimeOffset.Parse(rebaseTo!)
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
                                EntityIds = entitiesToRebase,
                                RebaseTo = rebaseTo?.ToLower() == "now" ? DateTimeOffset.Now : DateTimeOffset.Parse(rebaseTo!)
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
}
