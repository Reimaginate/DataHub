using System.Diagnostics;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Reimaginate.DataHub.CLI.Tools.Commands.Merge;
using Reimaginate.DataHub.CLI.Tools.Commands.Sync;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;
using Path = System.IO.Path;

namespace Reimaginate.DataHub.CLI.Tools.Helpers;

public static class CliHelpers
{
    public static List<string> ParseDisplayProps(string defaultProps, string? specificProps = null, string? additionalProps = null)
    {
        var props = (specificProps ?? defaultProps).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
        if (!string.IsNullOrEmpty(additionalProps))
        {
            props.AddRange(additionalProps.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
        }

        return props.Distinct(StringComparer.Ordinal).ToList();
    }

    #region Private Helpers

    private static void SaveToExcel<TData>(List<TData> results, string path, List<string> props)
    {
        using var excelWorkbook = new XLWorkbook();

        var data = results.ToDataTable(props);


        var dataSheet = excelWorkbook.Worksheets.Add("Results", 0);
        var table = dataSheet.Cell(1, 1).InsertTable(data, "Data");
        table.Rows().Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        dataSheet.Columns().AdjustToContents();


        var filePath = Path.Combine(path);
        excelWorkbook.SaveAs(filePath);
    }

    private static void ShowDeleteLogFailures(List<DeleteLogFailure> failures, string filePrefix, string label)
    {
        BulkOperationDetailsPrompt.Show(
            failures,
            [
                nameof(DeleteLogFailure.LogEntryId),
                nameof(DeleteLogFailure.FailureReason)
            ],
            filePrefix,
            label);
    }

    private static void ShowDeleteEntityFailures(List<DeleteDataHubEntityFailure> failures, string filePrefix)
    {
        BulkOperationDetailsPrompt.Show(
            failures,
            [
                $"{nameof(DeleteDataHubEntityFailure.DataHubEntity)}.{nameof(DataHubEntity.entityType)}",
                $"{nameof(DeleteDataHubEntityFailure.DataHubEntity)}.{nameof(DataHubEntity.id)}",
                nameof(DeleteDataHubEntityFailure.FailureReason)
            ],
            filePrefix,
            "failed entity delete result(s)");
    }

    private static void ShowRebaseFailures(List<RebaseEntityTrackingResult> failures, string filePrefix)
    {
        BulkOperationDetailsPrompt.Show(
            failures,
            [
                nameof(RebaseEntityTrackingResult.DataSource),
                nameof(RebaseEntityTrackingResult.EntityType),
                nameof(RebaseEntityTrackingResult.EntityId),
                nameof(RebaseEntityTrackingResult.FailureReason)
            ],
            filePrefix,
            "failed rebase result(s)");
    }

    private static void ShowExceptionFailures(List<Exception> failures, string filePrefix, string label)
    {
        BulkOperationDetailsPrompt.Show(
            failures.Select(ExceptionDetail.From).ToList(),
            [
                nameof(ExceptionDetail.Type),
                nameof(ExceptionDetail.Message)
            ],
            filePrefix,
            label);
    }

    private sealed class ExceptionDetail
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public static ExceptionDetail From(Exception exception)
        {
            return new ExceptionDetail
            {
                Type = exception.GetType().Name,
                Message = exception.Message
            };
        }
    }


    #endregion


    public static async Task<int?> ProcessDeleteTrackingData(bool delete, Func<Task<List<ChangeTrackingEntry>>> entriesFunc, IServiceProvider serviceProvider, bool skipConfirmation = false)
    {
        if (delete)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Foreground = Color.Red;

            if (!skipConfirmation && !AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
            {
                AnsiConsole.WriteLine();
                return 1;
            }

            AnsiConsole.ResetColors();
            AnsiConsole.WriteLine();

            AnsiConsole.Markup("[green]Preparing to delete...[/]");

            var entries = await entriesFunc();

            var failures = new List<DeleteTrackingDataEntryFailure>();

            await CliProgressHelper.RunAsync(async ctx =>
                {
                    var entryIds = entries.Select(s => s.id).ToList();
                    var totalCount = entryIds.Count;

                    var task = ctx.AddTask($"Deleting {entryIds.Count} entries");
                    task.StartTask();

                    while (entryIds.Any())
                    {
                        var batch = entryIds.Take(500).ToList();

                        var req = new SerializedRequest()
                        {
                            RequestType = nameof(DeleteTrackingEntriesRequest),
                            Data = JsonConvert.SerializeObject(new DeleteTrackingEntriesRequest()
                            {
                                TrackingEntryIds = batch
                            })
                        };
                        var cliApi = serviceProvider.GetRequiredService<ICLIApi>();
                        var deleteEntitiesResponse = await cliApi.PostAdminMessage<DeleteTrackingEntriesResponse>(req);
                        failures.AddRange(deleteEntitiesResponse.Failures ?? []);

                        var batchPercent = Convert.ToDouble(batch.Count) / Convert.ToDouble(totalCount) * 100;
                        task.Increment(batchPercent);

                        entryIds.RemoveRange(0, batch.Count);
                    }

                    task.Value = 100;
                    task.StopTask();

                });

            var deletedCount = Math.Max(entries.Count - failures.Count, 0);
            AnsiConsole.WriteLine($"Deleted {deletedCount} of {entries.Count} tracking entries. Failed {failures.Count}.");
            BulkOperationDetailsPrompt.Show(
                failures,
                [
                    nameof(DeleteTrackingDataEntryFailure.Id),
                    nameof(DeleteTrackingDataEntryFailure.FailureReason)
                ],
                "tracking-delete-errors",
                "failed tracking delete result(s)");

            return failures.Any() ? 0 : 1;
        }

        return null;
    }
    
    public static async Task<int?> ProcessDeleteMergeFailures(bool delete, IServiceProvider serviceProvider, string? where)
    {
        if (delete)
        {
            var failures = new List<DeleteLogFailure>();
            AnsiConsole.WriteLine();

            if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
            {
                AnsiConsole.WriteLine();
                return 1;
            }

            AnsiConsole.Markup("[lime]Deleting merge failures (press esc to cancel)[/]");

            await CliProgressHelper
                .RunAsync(async ctx =>
                {
                    var initTask = ctx.AddTask($"Preparing");
                    initTask.StartTask();

                    var req = new GetMergeFailuresWhereRequest()
                    {
                        Select = "x.id",
                        WhereClause = where,
                        PageSize = 1000,
                        GetTotalResultCount = true
                    };

                    var cliApi = serviceProvider.GetRequiredService<ICLIApi>();

                    var response = await cliApi.PostAdminMessage<GetMergeFailuresResponse>(new SerializedRequest()
                    {
                        RequestType = nameof(GetMergeFailuresWhereRequest),
                        Data = JsonConvert.SerializeObject(req)
                    });

                    initTask.Value = 100;
                    initTask.StopTask();

                    if (!response.Results.Any()) return;

                    var totalCount = response.ResultCount;

                    var deleteTask = ctx.AddTask($"Processing {totalCount} records");
                    deleteTask.StartTask();

                    while (response.Results.Any())
                    {
                        var pageIds = response.Results.Select(s => s.Id).ToList();
                        var deleteReq = new SerializedRequest()
                        {
                            RequestType = nameof(DeleteMergeFailuresRequest),
                            Data = JsonConvert.SerializeObject(new DeleteMergeFailuresRequest()
                            {
                                MergeFailureIds = pageIds
                            })
                        };

                        var deleteResponse = await cliApi.PostAdminMessage<DeleteMergeFailuresResponse>(deleteReq);
                        var pageFailures = deleteResponse.DeleteFailures ?? new List<DeleteLogFailure>();
                        if (!deleteResponse.Success)
                        {
                            failures.AddRange(pageFailures);
                        }

                        var deletedCount = deleteResponse.Success ? pageIds.Count : Math.Max(pageIds.Count - pageFailures.Count, 0);
                        var batchPercent = Convert.ToDouble(deletedCount) / Convert.ToDouble(totalCount) * 100;
                        deleteTask.Increment(batchPercent);

                        if (!response.MoreResultsAvailable || deletedCount == 0 || pageFailures.Any())
                        {
                            break;
                        }

                        req.ContinuationToken = null;
                        req.GetTotalResultCount = false;

                        response = await cliApi.PostAdminMessage<GetMergeFailuresResponse>(new SerializedRequest()
                        {
                            RequestType = nameof(GetMergeFailuresWhereRequest),
                            Data = JsonConvert.SerializeObject(req)
                        });
                    }

                    deleteTask.Value = 100;
                    deleteTask.StopTask();
                });

            AnsiConsole.WriteLine(failures.Any() ? $"{failures.Count} merge failure delete(s) failed." : "Merge failure delete completed.");
            ShowDeleteLogFailures(failures, "merge-failures-delete-errors", "failed merge failure delete result(s)");
            return failures.Any() ? 0 : 1;
        }

        return null;
    }
    
    public static async Task<int?> ProcessDeleteSyncFailures(bool delete, IServiceProvider serviceProvider, string? where)
    {
        if (delete)
        {
            var failures = new List<DeleteLogFailure>();
            AnsiConsole.WriteLine();

            if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
            {
                AnsiConsole.WriteLine();
                return 1;
            }

            AnsiConsole.Markup("[yellow]Deleting sync failures (press esc to cancel)[/]");

            await CliProgressHelper
                .RunAsync(async ctx =>
                {
                    var initTask = ctx.AddTask($"Preparing");
                    initTask.StartTask();

                    var req = new GetSyncFailuresWhereRequest()
                    {
                        Select = "x.id",
                        WhereClause = where,
                        PageSize = 1000,
                        GetTotalResultCount = true
                    };

                    var cliApi = serviceProvider.GetRequiredService<ICLIApi>();

                    var response = await cliApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                    {
                        RequestType = nameof(GetSyncFailuresWhereRequest),
                        Data = JsonConvert.SerializeObject(req)
                    });

                    initTask.Value = 100;
                    initTask.StopTask();

                    if (!response.Results.Any()) return;

                    var totalCount = response.ResultCount;

                    var deleteTask = ctx.AddTask($"Processing {totalCount} records");
                    deleteTask.StartTask();

                    while (response.Results.Any())
                    {
                        var pageIds = response.Results.Select(s => s.Id).ToList();
                        var deleteReq = new SerializedRequest()
                        {
                            RequestType = nameof(DeleteSyncFailuresRequest),
                            Data = JsonConvert.SerializeObject(new DeleteSyncFailuresRequest()
                            {
                                SyncFailureIds = pageIds
                            })
                        };

                        var deleteResponse = await cliApi.PostAdminMessage<DeleteSyncFailuresResponse>(deleteReq);
                        var pageFailures = deleteResponse.DeleteFailures ?? new List<DeleteLogFailure>();
                        if (!deleteResponse.Success)
                        {
                            failures.AddRange(pageFailures);
                        }

                        var deletedCount = deleteResponse.Success ? pageIds.Count : Math.Max(pageIds.Count - pageFailures.Count, 0);
                        var batchPercent = Convert.ToDouble(deletedCount) / Convert.ToDouble(totalCount) * 100;
                        deleteTask.Increment(batchPercent);

                        if (!response.MoreResultsAvailable || deletedCount == 0 || pageFailures.Any())
                        {
                            break;
                        }

                        req.ContinuationToken = null;
                        req.GetTotalResultCount = false;

                        response = await cliApi.PostAdminMessage<GetSyncFailuresResponse>(new SerializedRequest()
                        {
                            RequestType = nameof(GetSyncFailuresWhereRequest),
                            Data = JsonConvert.SerializeObject(req)
                        });
                    }

                    deleteTask.Value = 100;
                    deleteTask.StopTask();
                });

            AnsiConsole.WriteLine(failures.Any() ? $"{failures.Count} sync failure delete(s) failed." : "Sync failure delete completed.");
            ShowDeleteLogFailures(failures, "sync-failures-delete-errors", "failed sync failure delete result(s)");
            return failures.Any() ? 0 : 1;
        }

        return null;
    }
    
    public static async Task<int?> ProcessRetrySyncFailures(bool retry, IServiceProvider serviceProvider, Func<Task<List<SyncFailureDTO>>> syncFailuresFunc)
    {
        if (retry)
        {
            var result = 1;

            var results = await syncFailuresFunc();

            var entityTypeGroups = results.GroupBy(g => new { g.DataSource, g.DataHubEntityType });
            foreach (var entityTypeGroup in entityTypeGroups)
            {
                var entityIds = entityTypeGroup.Select(s => s.DataHubEntityId).ToList();
                var syncCommand = serviceProvider.GetRequiredService<SyncCommand>();
                var retryResult = await syncCommand.HandleCommand(entityTypeGroup.Key.DataSource, entityTypeGroup.Key.DataHubEntityType, entityIds.ToArray(), CancellationToken.None);
                if (retryResult != 1)
                {
                    result = 0;
                }
            }

            return result;
        }

        return null;
    }

    public static async Task<int?> ProcessRetryMergeFailures(bool retry, IServiceProvider serviceProvider, Func<Task<List<MergeFailureDTO>>> mergeFailuresFunc)
    {
        if (retry)
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

        return null;
    }
  
    public static async Task<int?> ProcessInfo<TData>(bool info, Func<Task<List<TData>>> resultsFunc, string defaultProperties, Action<List<TData>, List<object>>? additionalSummaryFunc = null)
    {
        if (info)
        {
            var results = await resultsFunc();

            if (results is List<JObject> jObjectResults) return ProcessInfo(info, jObjectResults, results.Count, defaultProperties);

            var props = typeof(TData).GetProperties().Select(s => s.Name).OrderBy(o => o).ToList();

            var displayTable = new List<object>
            {
                new { Property = "Type", Value = typeof(TData).Name },
                new { Property = "Record Count", Value = results.Count },
                new { Property = "Default Properties", Value = defaultProperties},
                new { Property = "All Properties", Value = string.Join(",", props) }
            };

            if (additionalSummaryFunc != null)
            {
                additionalSummaryFunc(results, displayTable);
            }

            ConsoleHelper.PrintTable(displayTable, new List<string>() { "Property", "Value" });

            return 1;
        }

        return null;
    }

    public static async Task<int?> ProcessInfo<TRes, TData>(bool info, Func<Task<TRes>> responseFunc, Func<TRes, List<TData>> resultsFunc, Func<TRes, List<TData>, int> resultCountFunc, string defaultProperties, Action<List<TData>, List<object>>? additionalSummaryFunc = null)
    {
        if (info)
        {
            var response = await responseFunc();
            var results = resultsFunc(response);
            var resultCount = resultCountFunc(response, results);

            if (results is List<JObject> jObjectResults) return ProcessInfo(info, jObjectResults, resultCount, defaultProperties);

            var props = typeof(TData).GetProperties().Select(s => s.Name).OrderBy(o => o).ToList();

            var displayTable = new List<object>
            {
                new { Property = "Type", Value = typeof(TData).Name },
                new { Property = "Record Count", Value = resultCount },
                new { Property = "Default Properties", Value = defaultProperties},
                new { Property = "All Properties", Value = string.Join(",", props) }
            };

            if (additionalSummaryFunc != null)
            {
                additionalSummaryFunc(results, displayTable);
            }

            ConsoleHelper.PrintTable(displayTable, new List<string>() { "Property", "Value" });

            return 1;
        }

        return null;
    }
    public static int? ProcessInfo(bool info, List<JObject> results, int resultCount, string defaultProperties)
    {
        if (info)
        {
            var entityTypes = results.Select(s => s.Value<string>(nameof(DataHubEntity.entityType))).Distinct();

            var props = results.SelectMany(s => s.Children().Select(c => c.Path)).Distinct().OrderBy(o => o);
            var allProps = string.Join(",", props);

            var displayTable = new List<object>
            {
                new { Property = "Entity Types", Value = string.Join(",", entityTypes) },
                new { Property = "ResultCount", Value = resultCount },
                new { Property = "Default Properties", Value = defaultProperties},
                new { Property = "All Properties", Value = allProps }
            };

            ConsoleHelper.PrintTable(displayTable, new List<string>() { "Property", "Value" });

            return 1;
        }

        return null;
    }

    public static List<JObject> SummarizeResults(bool expandResults, List<JObject> results)
    {
        results ??= [];

        return expandResults ? results : results.Select(result =>
        {
            if (result.ContainsKey(nameof(DataHubEntity.lastUpdated)))
                result[nameof(DataHubEntity.lastUpdated)] = result[nameof(DataHubEntity.lastUpdated)]!.Value<DateTime?>()?.ToString("yyyy-MM-dd hh:mm:ss");

            if (result[nameof(DataHubEntity.alternateKeys)] is JArray alternateKeys)
                result[nameof(DataHubEntity.alternateKeys)] = SummarizeAlternateKeys(alternateKeys);

            return result;
        }).ToList();
    }

    private static string SummarizeAlternateKeys(JArray alternateKeys)
    {
        return string.Join(", ", alternateKeys
            .OfType<JObject>()
            .Select(alternateKey =>
            {
                var key = alternateKey.Value<string>(nameof(AlternateKey.Key)) ?? string.Empty;
                var value = alternateKey.Value<string>(nameof(AlternateKey.Value)) ?? string.Empty;
                return $"{key}={value}";
            }));
    }

    public static List<JObject> SummarizeResults<T>(bool expandResults, List<T> results)
    {
        results ??= [];

        return results.Select(result => JObject.FromObject(result!)).ToList();
    }

    public static async Task<int?> ProcessSyncToDataSource(string syncToDataSource, Func<Task<List<JObject>>> entitiesFunc, IServiceProvider serviceProvider)
    {
        if (!string.IsNullOrEmpty(syncToDataSource))
        {
            var parts = syncToDataSource.Split('/');
            if (parts.Length != 2)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("You must specify both the Data Source and Entity Type in the form DataSource/Entity Type");
                AnsiConsole.WriteLine();
                return 1;
            }

            var entities = await entitiesFunc();

            var dataSource = parts[0];
            var entityType = parts[1];
            var entityIds = entities.Select(s => s.Value<string>(nameof(DataHubEntity.id))).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).ToArray();

            var syncCommand = serviceProvider.GetRequiredService<SyncCommand>();
            return await syncCommand.HandleCommand(dataSource, entityType, entityIds, CancellationToken.None);
        }

        return null;
    }

    public static async Task<int?> ProcessMergeFromDataSource(string mergeFromDataSource, Func<Task<List<JObject>>> entitiesFunc, IServiceProvider serviceProvider)
    {
        if (!string.IsNullOrEmpty(mergeFromDataSource))
        {
            var parts = mergeFromDataSource.Split('/');
            if (parts.Length != 2)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("You must specify both the Data Source and Entity Type in the form DataSource/Entity Type");
                AnsiConsole.WriteLine();
                return 1;
            }


            var dataSource = parts[0];
            var entityType = parts[1];

            var entities = await entitiesFunc();

            var entityAlternateKeys = entities.SelectMany(s => s.Value<JArray>(nameof(DataHubEntity.alternateKeys))?.ToObject<List<AlternateKey>>() ?? []);
            var sourceEntityIds = entityAlternateKeys.Where(w => w.Key.ToLower().StartsWith($"{dataSource}.".ToLower())).Select(s => s.Value).ToList();

            if (!sourceEntityIds.Any())
            {
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("Could not determine source entity ids for the selected entities");
                AnsiConsole.WriteLine();
                return 0;
            }

            var mergeCommand = serviceProvider.GetRequiredService<MergeCommand>();
            return await mergeCommand.HandleCommand(dataSource, entityType, sourceEntityIds.ToArray(), CancellationToken.None);
        }

        return null;
    }
    
    // Updated

    public static async Task<int> DeleteDataHubEntitiesAsync(GetEntitiesWhereRequest whereReq, IServiceProvider serviceProvider)
    {

        AnsiConsole.WriteLine();
        AnsiConsole.Foreground = Color.Red;

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        AnsiConsole.ResetColors();
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]Press ESC to cancel[/]");

        whereReq.Select = "x.entityType,x.id";
        whereReq.GetTotalResultCount = true;
        var getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);
        var entities = getEntitiesResponse.Results;
        var totalCount = getEntitiesResponse.ResultCount;

        var groupedByEntityType = entities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

        var cancelled = false;
        var failures = new List<DeleteDataHubEntityFailure>();
        var deletedCount = 0;
        await CliProgressHelper.RunAsync(async ctx =>
            {
                var lastPageDeletedCount = 0;
                var lastPageHadFailures = false;

                var task = ctx.AddTask($"Deleting {totalCount} records", Math.Max(totalCount, 1));
                task.StartTask();

                foreach (var entityTypeGroup in groupedByEntityType)
                {
                    if (cancelled) break;

                    var entityIds = entityTypeGroup.Select(s => s.Value<string>(nameof(DataHubEntity.id))).ToList();

                    while (entityIds.Any())
                    {
                        if (cancelled) break;

                        var batch = entityIds.Take(5000).ToList();

                        var req = new SerializedRequest()
                        {
                            RequestType = nameof(DeleteDataHubEntitiesRequest),
                            Data = JsonConvert.SerializeObject(new DeleteDataHubEntitiesRequest()
                            {
                                EntityType = entityTypeGroup.Key,
                                EntityIds = batch,
                                IncludeTrackingEntries = true
                            })
                        };

                        var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                        var deleteEntitiesResponse = await adminApi.PostAdminMessage<DeleteDataHubEntitiesResponse>(req);
                        var batchFailures = deleteEntitiesResponse.Failures ?? new List<DeleteDataHubEntityFailure>();
                        failures.AddRange(batchFailures);
                        lastPageHadFailures = lastPageHadFailures || batchFailures.Any();

                        var batchDeletedCount = Math.Max(batch.Count - batchFailures.Count, 0);
                        deletedCount += batchDeletedCount;
                        lastPageDeletedCount += batchDeletedCount;
                        task.Increment(batchDeletedCount);

                        entityIds.RemoveRange(0, batch.Count);

                        while (Console.KeyAvailable)
                        {
                            var kp = Console.ReadKey();
                            if (kp.Key == ConsoleKey.Escape)
                            {
                                AnsiConsole.WriteLine("[red]Cancelling...");
                                cancelled = true;
                                break;
                            }
                        }
                    }
                }

                while (getEntitiesResponse.MoreResultsAvailable && !cancelled && lastPageDeletedCount > 0 && !lastPageHadFailures)
                {
                    lastPageDeletedCount = 0;
                    lastPageHadFailures = false;
                    whereReq.ContinuationToken = null;
                    whereReq.GetTotalResultCount = false;

                    getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);
                    entities = getEntitiesResponse.Results;

                    groupedByEntityType = entities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

                    foreach (var entityTypeGroup in groupedByEntityType)
                    {
                        if (cancelled) break;

                        var entityIds = entityTypeGroup.Select(s => s.Value<string>(nameof(DataHubEntity.id))).ToList();

                        while (entityIds.Any())
                        {
                            if (cancelled) break;

                            var batch = entityIds.Take(5000).ToList();

                            var req = new SerializedRequest()
                            {
                                RequestType = nameof(DeleteDataHubEntitiesRequest),
                                Data = JsonConvert.SerializeObject(new DeleteDataHubEntitiesRequest()
                                {
                                    EntityType = entityTypeGroup.Key,
                                    EntityIds = batch,
                                    IncludeTrackingEntries = true
                                })
                            };
                            var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                            var deleteEntitiesResponse = await adminApi.PostAdminMessage<DeleteDataHubEntitiesResponse>(req);
                            var batchFailures = deleteEntitiesResponse.Failures ?? new List<DeleteDataHubEntityFailure>();
                            failures.AddRange(batchFailures);
                            lastPageHadFailures = lastPageHadFailures || batchFailures.Any();

                            var batchDeletedCount = Math.Max(batch.Count - batchFailures.Count, 0);
                            deletedCount += batchDeletedCount;
                            lastPageDeletedCount += batchDeletedCount;
                            task.Increment(batchDeletedCount);

                            entityIds.RemoveRange(0, batch.Count);

                            while (Console.KeyAvailable)
                            {
                                var kp = Console.ReadKey();
                                if (kp.Key == ConsoleKey.Escape)
                                {
                                    AnsiConsole.MarkupLine("[red]Cancelling...[/]");
                                    cancelled = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                task.StopTask();
            });

        AnsiConsole.WriteLine($"Deleted {deletedCount} of {totalCount} DataHub entities. Failed {failures.Count}.");
        ShowDeleteEntityFailures(failures, "entities-delete-errors");
        return 1;
    }

    public static async Task<int> DeleteDataHubEntitiesAsync(Func<Task<List<JObject>>> entitiesFunc, IServiceProvider serviceProvider)
    {

        AnsiConsole.WriteLine();
        AnsiConsole.Foreground = Color.Red;

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        AnsiConsole.ResetColors();
        AnsiConsole.WriteLine();

        AnsiConsole.WriteLine("[green]Preparing to delete...");

        var entities = await entitiesFunc();

        var groupedByEntityType = entities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

        var failures = new List<DeleteDataHubEntityFailure>();

        var cancelled = false;

        await CliProgressHelper.RunAsync(async ctx =>
            {
                foreach (var entityTypeGroup in groupedByEntityType)
                {
                    if (cancelled) break;

                    var entityIds = entityTypeGroup.Select(s => s.Value<string>(nameof(DataHubEntity.id))).ToList();
                    var totalCount = entityIds.Count;

                    var task = ctx.AddTask($"Deleting {entityIds.Count} {entityTypeGroup.Key} records");
                    task.StartTask();

                    while (entityIds.Any())
                    {
                        if (cancelled) break;

                        var batch = entityIds.Take(500).ToList();

                        var req = new SerializedRequest()
                        {
                            RequestType = nameof(DeleteDataHubEntitiesRequest),
                            Data = JsonConvert.SerializeObject(new DeleteDataHubEntitiesRequest()
                            {
                                EntityType = entityTypeGroup.Key,
                                EntityIds = batch,
                                IncludeTrackingEntries = true
                            })
                        };
                        var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                        var deleteEntitiesResponse = await adminApi.PostAdminMessage<DeleteDataHubEntitiesResponse>(req);
                        failures.AddRange(deleteEntitiesResponse.Failures ?? []);

                        var batchPercent = Convert.ToDouble(batch.Count) / Convert.ToDouble(totalCount) * 100;
                        task.Increment(batchPercent);

                        entityIds.RemoveRange(0, batch.Count);

                        while (Console.KeyAvailable)
                        {
                            var kp = Console.ReadKey();
                            if (kp.Key == ConsoleKey.Escape)
                            {
                                AnsiConsole.WriteLine("[red]Cancelling...");
                                cancelled = true;
                                break;
                            }
                        }
                    }

                    task.StopTask();
                }
            });

        AnsiConsole.WriteLine($"Deleted {Math.Max(entities.Count - failures.Count, 0)} of {entities.Count} DataHub entities. Failed {failures.Count}.");
        ShowDeleteEntityFailures(failures, "entities-delete-errors");
        return failures.Any() ? 0 : 1;

    }

    public static async Task<int> DetachFromDataSourceAsync(GetEntitiesWhereRequest whereReq, string datasource, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {

        AnsiConsole.WriteLine();
        AnsiConsole.Foreground = Color.Red;

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        AnsiConsole.ResetColors();
        AnsiConsole.WriteLine();

        whereReq.Select = "x.entityType,x.id";
        whereReq.GetTotalResultCount = true;
        whereReq.PageSize = 5000;

        var getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);
        var entities = getEntitiesResponse.Results;
        var totalCount = getEntitiesResponse.ResultCount;

        var groupedByEntityType = entities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

        var failures = new List<Exception>();
        var detachedCount = 0;
        await CliProgressHelper.RunAsync(async ctx =>
            {
                var task = ctx.AddTask($"Detaching {totalCount} records", Math.Max(totalCount, 1));
                task.StartTask();

                foreach (var entityTypeGroup in groupedByEntityType)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var entityIds = entityTypeGroup.Select(s => s.Value<string>(nameof(DataHubEntity.id))).ToList();

                    while (entityIds.Any())
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var batch = entityIds.Take(1000).ToList();

                        var req = new SerializedRequest()
                        {
                            RequestType = nameof(DetachEntitiesRequest),
                            Data = JsonConvert.SerializeObject(new DetachEntitiesRequest()
                            {
                                EntityType = entityTypeGroup.Key,
                                EntityIds = batch,
                                DataSource = datasource

                            })
                        };

                        var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                        var detachEntitiesResponse = await adminApi.PostAdminMessage<DetachEntitiesResponse>(req, cancellationToken);
                        failures.AddRange(detachEntitiesResponse.Failures ?? []);
                        detachedCount += batch.Count;

                        task.Increment(batch.Count);

                        entityIds.RemoveRange(0, batch.Count);
                    }
                }

                while (getEntitiesResponse.MoreResultsAvailable && !cancellationToken.IsCancellationRequested)
                {
                    whereReq.ContinuationToken = getEntitiesResponse.ContinuationToken;
                    whereReq.GetTotalResultCount = false;

                    getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);
                    entities = getEntitiesResponse.Results;

                    groupedByEntityType = entities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

                    foreach (var entityTypeGroup in groupedByEntityType)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var entityIds = entityTypeGroup.Select(s => s.Value<string>(nameof(DataHubEntity.id))).ToList();

                        while (entityIds.Any())
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            var batch = entityIds.Take(1000).ToList();

                            var req = new SerializedRequest()
                            {
                                RequestType = nameof(DetachEntitiesRequest),
                                Data = JsonConvert.SerializeObject(new DetachEntitiesRequest()
                                {
                                    EntityType = entityTypeGroup.Key,
                                    EntityIds = batch,
                                    DataSource = datasource
                                })
                            };
                            var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                            var detachEntitiesResponse = await adminApi.PostAdminMessage<DetachEntitiesResponse>(req, cancellationToken);
                            failures.AddRange(detachEntitiesResponse.Failures ?? []);
                            detachedCount += batch.Count;

                            task.Increment(batch.Count);

                            entityIds.RemoveRange(0, batch.Count);

                        }
                    }
                }

                task.StopTask();
            });

        AnsiConsole.WriteLine($"Detached {Math.Max(detachedCount - failures.Count, 0)} of {totalCount} DataHub entities from {datasource}. Failed {failures.Count}.");
        ShowExceptionFailures(failures, "entities-detach-errors", "failed detach result(s)");
        return failures.Any() ? 0 : 1;
    }

    public static async Task<int> DetachFromDataSourceAsync(Func<Task<List<JObject>>> entitiesFunc, string datasource, IServiceProvider serviceProvider, List<string> displayProps)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Foreground = Color.Red;

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN DUPLICATION OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        AnsiConsole.ResetColors();

        var entities = await entitiesFunc();
        var groupedByEntityType = entities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

        var resultingEntities = new List<JObject>();
        var failures = new List<Exception>();

        foreach (var entityTypeGroup in groupedByEntityType)
        {
            var req = new SerializedRequest()
            {
                RequestType = nameof(DetachEntitiesRequest),
                Data = JsonConvert.SerializeObject(new DetachEntitiesRequest()
                {
                    EntityType = entityTypeGroup.Key,
                    EntityIds = entityTypeGroup.Select(s => s.Value<string>(nameof(DataHubEntity.id))).ToList(),
                    DataSource = datasource
                })
            };

            var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
            var detachEntitiesResponse = await adminApi.PostAdminMessage<DetachEntitiesResponse>(req);

            resultingEntities.AddRange(detachEntitiesResponse.ResultingEntities ?? []);
            failures.AddRange(detachEntitiesResponse.Failures ?? []);
        }

        AnsiConsole.WriteLine($"Detached {resultingEntities.Count} DataHub entities from {datasource}. Failed {failures.Count}.");
        ShowExceptionFailures(failures, "entities-detach-errors", "failed detach result(s)");
        AnsiConsole.WriteLine();
        return failures.Any() ? 0 : 1;
    }

    public static async Task<int> RebaseEntitiesAsync(Func<Task<List<JObject>>> dataHubEntities, string rebaseTo, IServiceProvider serviceProvider)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Foreground = Color.Red;

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        AnsiConsole.ResetColors();
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[white]Press ESC to cancel[/]");

        var entities = await dataHubEntities();
        var totalCount = entities.Count;

        var groupedByEntityType = entities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

        var cancelled = false;
        var failures = new List<RebaseEntityTrackingResult>();
        await CliProgressHelper.RunAsync(async ctx =>
            {
                var task = ctx.AddTask($"Patching {totalCount} records", Math.Max(totalCount, 1));
                task.StartTask();

                foreach (var entityTypeGroup in groupedByEntityType)
                {
                    if (cancelled) break;

                    var entityIds = entityTypeGroup.Select(s => s.Value<string>(nameof(DataHubEntity.id))).ToList();

                    while (entityIds.Any())
                    {
                        if (cancelled) break;

                        var batch = entityIds.Take(5000).ToList();

                        var req = new SerializedRequest()
                        {
                            RequestType = nameof(RebaseDataHubEntitiesRequest),
                            Data = JsonConvert.SerializeObject(new RebaseDataHubEntitiesRequest()
                            {
                                EntityType = entityTypeGroup.Key,
                                EntityIds = batch,
                                RebaseTo = rebaseTo?.ToLower() == "now" ? DateTimeOffset.Now : DateTimeOffset.Parse(rebaseTo!)
                            })
                        };
                        var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                        var rebaseEntitiesResponse = await adminApi.PostAdminMessage<RebaseDataHubEntitiesResponse>(req);
                        failures.AddRange(rebaseEntitiesResponse.Results.Where(w => !w.Success));

                        task.Increment(batch.Count);

                        entityIds.RemoveRange(0, batch.Count);

                        while (Console.KeyAvailable)
                        {
                            var kp = Console.ReadKey();
                            if (kp.Key == ConsoleKey.Escape)
                            {
                                AnsiConsole.WriteLine("[red]Cancelling...");
                                cancelled = true;
                                break;
                            }
                        }
                    }
                }

                task.StopTask();
            });

        AnsiConsole.WriteLine($"Rebased {Math.Max(totalCount - failures.Count, 0)} of {totalCount} DataHub entities. Failed {failures.Count}.");
        ShowRebaseFailures(failures, "entities-rebase-errors");
        return 1;

    }

    public static async Task<int> RebaseEntitiesAsync(GetEntitiesWhereRequest whereReq, string rebaseTo, IServiceProvider serviceProvider)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Foreground = Color.Red;

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        AnsiConsole.ResetColors();
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[white]Press ESC to cancel[/]");

        whereReq.Select = "x.entityType,x.id";
        whereReq.GetTotalResultCount = true;
        var getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);
        var entities = getEntitiesResponse.Results;
        var totalCount = getEntitiesResponse.ResultCount;

        var groupedByEntityType = entities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

        var cancelled = false;
        var failures = new List<RebaseEntityTrackingResult>();
        await CliProgressHelper.RunAsync(async ctx =>
            {
                var task = ctx.AddTask($"Rebasing {totalCount} records", Math.Max(totalCount, 1));
                task.StartTask();

                foreach (var entityTypeGroup in groupedByEntityType)
                {
                    if (cancelled) break;

                    var entityIds = entityTypeGroup.Select(s => s.Value<string>(nameof(DataHubEntity.id))).ToList();

                    while (entityIds.Any())
                    {
                        if (cancelled) break;

                        var batch = entityIds.Take(5000).ToList();

                        var req = new SerializedRequest()
                        {
                            RequestType = nameof(RebaseDataHubEntitiesRequest),
                            Data = JsonConvert.SerializeObject(new RebaseDataHubEntitiesRequest()
                            {
                                EntityType = entityTypeGroup.Key,
                                EntityIds = batch,
                                RebaseTo = rebaseTo?.ToLower() == "now" ? DateTimeOffset.Now : DateTimeOffset.Parse(rebaseTo!)
                            })
                        };
                        var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                        var rebaseEntitiesResponse = await adminApi.PostAdminMessage<RebaseDataHubEntitiesResponse>(req);
                        failures.AddRange(rebaseEntitiesResponse.Results.Where(w => !w.Success));

                        task.Increment(batch.Count);

                        entityIds.RemoveRange(0, batch.Count);

                        while (Console.KeyAvailable)
                        {
                            var kp = Console.ReadKey();
                            if (kp.Key == ConsoleKey.Escape)
                            {
                                AnsiConsole.WriteLine("[red]Cancelling...");
                                cancelled = true;
                                break;
                            }
                        }
                    }
                }

                while (getEntitiesResponse.MoreResultsAvailable && !cancelled)
                {
                    whereReq.ContinuationToken = getEntitiesResponse.ContinuationToken;
                    whereReq.GetTotalResultCount = false;

                    getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);
                    entities = getEntitiesResponse.Results;

                    groupedByEntityType = entities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

                    foreach (var entityTypeGroup in groupedByEntityType)
                    {
                        if (cancelled) break;

                        var entityIds = entityTypeGroup.Select(s => s.Value<string>(nameof(DataHubEntity.id))).ToList();

                        while (entityIds.Any())
                        {
                            if (cancelled) break;

                            var batch = entityIds.Take(5000).ToList();

                            var req = new SerializedRequest()
                            {
                                RequestType = nameof(RebaseDataHubEntitiesRequest),
                                Data = JsonConvert.SerializeObject(new RebaseDataHubEntitiesRequest()
                                {
                                    EntityType = entityTypeGroup.Key,
                                    EntityIds = batch,
                                    RebaseTo = rebaseTo?.ToLower() == "now" ? DateTimeOffset.Now : DateTimeOffset.Parse(rebaseTo!)
                                })
                            };
                            var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                            var patchEntitiesResponse = await adminApi.PostAdminMessage<RebaseDataHubEntitiesResponse>(req);
                            failures.AddRange(patchEntitiesResponse.Results.Where(w => !w.Success));

                            task.Increment(batch.Count);

                            entityIds.RemoveRange(0, batch.Count);

                            while (Console.KeyAvailable)
                            {
                                var kp = Console.ReadKey();
                                if (kp.Key == ConsoleKey.Escape)
                                {
                                    AnsiConsole.MarkupLine("[red]Cancelling...[/]");
                                    cancelled = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                task.StopTask();
            });

        AnsiConsole.WriteLine($"Rebased {Math.Max(totalCount - failures.Count, 0)} of {totalCount} DataHub entities. Failed {failures.Count}.");
        ShowRebaseFailures(failures, "entities-rebase-errors");
        return 1;
    }
    
    public static async Task<int> RebaseSourceEntitiesAsync(GetEntitiesWhereRequest whereReq, string dataSource, string rebaseTo, IServiceProvider serviceProvider)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Foreground = Color.Red;

        if (!AnsiConsole.Confirm("[bold red]WARNING: THIS IS A HIGH RISK OPERATION THAT MAY RESULT IN LOSS OF DATA. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        AnsiConsole.ResetColors();
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[white]Press ESC to cancel[/]");

        whereReq.Select = "x.entityType,x.id,x.alternateKeys";
        whereReq.GetTotalResultCount = true;
        var getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);
        var dataHubEntities = getEntitiesResponse.Results;
        var totalCount = getEntitiesResponse.ResultCount;

        var groupedByEntityType = dataHubEntities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

        var cancelled = false;
        var failures = new List<RebaseEntityTrackingResult>();
        await CliProgressHelper.RunAsync(async ctx =>
            {
                var task = ctx.AddTask($"Rebasing {totalCount} records", Math.Max(totalCount, 1));
                task.StartTask();

                foreach (var entityTypeGroup in groupedByEntityType)
                {
                    if (cancelled) break;

                    var entities = entityTypeGroup.ToList();
                    var alternateKeys = entities.Where(w=>w.ContainsKey(nameof(DataHubEntity.alternateKeys))).Select(s => s.Value<JArray>(nameof(DataHubEntity.alternateKeys))).SelectMany(s=>s?.Children() ?? []).ToList();
                    var sourceSystemAltKeys = alternateKeys.Where(w => (w.Value<string>(nameof(AlternateKey.Key)) ?? string.Empty).ToLower().StartsWith($"{dataSource.ToLower()}."));
                   
                    var srcEntityTypeGroups = sourceSystemAltKeys.GroupBy(g =>
                    {
                        var parts = (g.Value<string>(nameof(AlternateKey.Key)) ?? string.Empty).Split('.');
                        var entityType = parts[1];
                        return new { EntityType = entityType };
                    });

                    foreach (var srcEntityTypeGroup in srcEntityTypeGroups)
                    {
                        if (cancelled) break;

                        var entityIds = srcEntityTypeGroup.Select(s => s.Value<string>(nameof(AlternateKey.Value))).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).ToList();

                        while (entityIds.Any())
                        {
                            if (cancelled) break;

                            var batch = entityIds.Take(5000).ToList();

                            var req = new SerializedRequest()
                            {
                                RequestType = nameof(RebaseSourceEntitiesRequest),
                                Data = JsonConvert.SerializeObject(new RebaseSourceEntitiesRequest()
                                {
                                    DataSource = dataSource,
                                    EntityType = srcEntityTypeGroup.Key.EntityType,
                                    EntityIds = batch,
                                    RebaseTo = rebaseTo?.ToLower() == "now" ? DateTimeOffset.Now : DateTimeOffset.Parse(rebaseTo!)
                                })
                            };
                            var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                            var rebaseEntitiesResponse = await adminApi.PostAdminMessage<RebaseSourceEntitiesResponse>(req);
                            failures.AddRange(rebaseEntitiesResponse.Results.Where(w => !w.Success));

                            task.Increment(batch.Count);

                            entityIds.RemoveRange(0, batch.Count);

                            while (Console.KeyAvailable)
                            {
                                var kp = Console.ReadKey();
                                if (kp.Key == ConsoleKey.Escape)
                                {
                                    AnsiConsole.WriteLine("[red]Cancelling...");
                                    cancelled = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                while (getEntitiesResponse.MoreResultsAvailable && !cancelled)
                {
                    whereReq.ContinuationToken = getEntitiesResponse.ContinuationToken;
                    whereReq.GetTotalResultCount = false;

                    getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);
                    dataHubEntities = getEntitiesResponse.Results;

                    groupedByEntityType = dataHubEntities.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));

                    foreach (var entityTypeGroup in groupedByEntityType)
                    {
                        if (cancelled) break;

                        var entities = entityTypeGroup.ToList();
                        var alternateKeys = entities.Where(w => w.ContainsKey(nameof(DataHubEntity.alternateKeys))).Select(s => s.Value<JArray>(nameof(DataHubEntity.alternateKeys))).SelectMany(s => s?.Children() ?? []).ToList();
                        var sourceSystemAltKeys = alternateKeys.Where(w => (w.Value<string>(nameof(AlternateKey.Key)) ?? string.Empty).ToLower().StartsWith($"{dataSource.ToLower()}."));

                        var srcEntityTypeGroups = sourceSystemAltKeys.GroupBy(g =>
                        {
                            var parts = (g.Value<string>(nameof(AlternateKey.Key)) ?? string.Empty).Split('.');
                            var entityType = parts[1];
                            return new { EntityType = entityType };
                        });

                        foreach (var srcEntityTypeGroup in srcEntityTypeGroups)
                        {
                            if (cancelled) break;

                            var entityIds = srcEntityTypeGroup.Select(s => s.Value<string>(nameof(AlternateKey.Value))).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).ToList();

                            while (entityIds.Any())
                            {
                                if (cancelled) break;

                                var batch = entityIds.Take(5000).ToList();

                                var req = new SerializedRequest()
                                {
                                    RequestType = nameof(RebaseSourceEntitiesRequest),
                                    Data = JsonConvert.SerializeObject(new RebaseSourceEntitiesRequest()
                                    {
                                        DataSource = dataSource,
                                        EntityType = srcEntityTypeGroup.Key.EntityType,
                                        EntityIds = batch,
                                        RebaseTo = rebaseTo?.ToLower() == "now" ? DateTimeOffset.Now : DateTimeOffset.Parse(rebaseTo!)
                                    })
                                };
                                var adminApi = serviceProvider.GetRequiredService<ICLIApi>();
                                var rebaseEntitiesResponse = await adminApi.PostAdminMessage<RebaseSourceEntitiesResponse>(req);
                                failures.AddRange(rebaseEntitiesResponse.Results.Where(w => !w.Success));

                                task.Increment(batch.Count);

                                entityIds.RemoveRange(0, batch.Count);

                                while (Console.KeyAvailable)
                                {
                                    var kp = Console.ReadKey();
                                    if (kp.Key == ConsoleKey.Escape)
                                    {
                                        AnsiConsole.WriteLine("[red]Cancelling...");
                                        cancelled = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                task.StopTask();
            });

        AnsiConsole.WriteLine($"Rebased source entities for {Math.Max(totalCount - failures.Count, 0)} of {totalCount} DataHub entities. Failed {failures.Count}.");
        ShowRebaseFailures(failures, "source-entities-rebase-errors");
        return 1;
    }
    
    public static async Task<int> SaveToAsync(GetAlternateKeysWhereRequest whereReq, string saveTo, bool openFile, IServiceProvider serviceProvider)
    {
        AnsiConsole.Markup($"[lime]Saving entities to {saveTo} (press esc to cancel)[/]");

        var ext = Path.GetExtension(saveTo).Trim('.');

        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt));

        try
        {
            if (File.Exists(saveTo))
            {
                AnsiConsole.WriteLine();
                var deleteConfirm = AnsiConsole.Confirm($"[bold red]File {saveTo} already exists. Overwrite file?[/]", false);
                if (!deleteConfirm)
                {
                    AnsiConsole.MarkupLine("[yellow]write aborted[/]");
                    return 0;
                }

                File.Delete(saveTo);
            }

            await using var file = File.CreateText(saveTo!);

            await CliProgressHelper
                .RunAsync(async ctx =>
                {
                    var prepTask = ctx.AddTask("Retrieving query stats");

                    whereReq.GetTotalResultCount = true;
                    whereReq.PageSize = 1;
                    var getEntitiesResponse = await RetrievePagedResultsAsync<GetAlternateKeysWhereRequest, GetAlternateKeysResponse>(serviceProvider, whereReq);

                    prepTask.Value = 100;


                    var totalCount = getEntitiesResponse.ResultCount;
                    var consoleTask = ctx.AddTask($"Saving ({totalCount} records)");
                    consoleTask.MaxValue = Math.Max(totalCount, 1);

                    consoleTask.StartTask();

                    foreach (var result in getEntitiesResponse.Results.Select(JObject.FromObject))
                    {
                        await file.WriteLineAsync(result.ToString(Formatting.None));
                    }

                    consoleTask.Increment(getEntitiesResponse.Results.Count());

                    while (getEntitiesResponse.MoreResultsAvailable)
                    {
                        whereReq.PageSize = 5000;
                        whereReq.GetTotalResultCount = false;
                        whereReq.ContinuationToken = getEntitiesResponse.ContinuationToken;

                        await retryPolicy.Execute(async () =>
                        {
                            getEntitiesResponse = await RetrievePagedResultsAsync<GetAlternateKeysWhereRequest, GetAlternateKeysResponse>(serviceProvider, whereReq);
                        });

                        foreach (var result in getEntitiesResponse.Results.Select(JObject.FromObject))
                        {
                            await file.WriteLineAsync(result.ToString(Formatting.None));
                        }

                        consoleTask.Increment(getEntitiesResponse.Results.Count());
                    }
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Something went wrong: {0}[/]", ex.Message);
            return 0;
        }

        return 1;
    }
    
    public static async Task<int> SaveToAsync(GetEntitiesWhereRequest whereReq, string saveTo, bool openFile, IServiceProvider serviceProvider)
    {
        AnsiConsole.Markup($"[lime]Saving entities to {saveTo} (press esc to cancel)[/]");

        var ext = Path.GetExtension(saveTo).Trim('.');

        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt));

        try
        {
            if (File.Exists(saveTo))
            {
                AnsiConsole.WriteLine();
                var deleteConfirm = AnsiConsole.Confirm($"[bold red]File {saveTo} already exists. Overwrite file?[/]", false);
                if (!deleteConfirm)
                {
                    AnsiConsole.MarkupLine("[yellow]write aborted[/]");
                    return 0;
                }

                File.Delete(saveTo);
            }

            await using var file = File.CreateText(saveTo!);

            await CliProgressHelper
                .RunAsync(async ctx =>
                {
                    var prepTask = ctx.AddTask("Retrieving query stats");

                    whereReq.GetTotalResultCount = true;
                    whereReq.PageSize = 1;
                    var getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);

                    prepTask.Value = 100;


                    var totalCount = getEntitiesResponse.ResultCount;
                    var consoleTask = ctx.AddTask($"Saving ({totalCount} records)");
                    consoleTask.MaxValue = Math.Max(totalCount, 1);

                    consoleTask.StartTask();

                    foreach (var result in getEntitiesResponse.Results)
                    {
                        await file.WriteLineAsync(result.ToString(Formatting.None));
                    }

                    consoleTask.Increment(getEntitiesResponse.Results.Count());

                    while (getEntitiesResponse.MoreResultsAvailable)
                    {
                        whereReq.PageSize = 5000;
                        whereReq.GetTotalResultCount = false;
                        whereReq.ContinuationToken = getEntitiesResponse.ContinuationToken;

                        await retryPolicy.Execute(async () =>
                        {
                            getEntitiesResponse = await RetrievePagedResultsAsync<GetEntitiesWhereRequest, GetEntitiesResponse>(serviceProvider, whereReq);
                        });

                        foreach (var result in getEntitiesResponse.Results)
                        {
                            await file.WriteLineAsync(result.ToString(Formatting.None));
                        }

                        consoleTask.Increment(getEntitiesResponse.Results.Count());
                    }
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Something went wrong: {0}[/]", ex.Message);
            return 0;
        }

        return 1;
    }

    public static async Task<int?> SaveToAsync<TData>(string saveTo, Func<Task<List<TData>>> resultsFunc, List<string> props, bool dontOpen)
    {
        if (!string.IsNullOrEmpty(saveTo))
        {
            var ext = Path.GetExtension(saveTo).Trim('.');

            if (File.Exists(saveTo))
            {
                AnsiConsole.WriteLine();
                var deleteConfirm = AnsiConsole.Confirm($"[bold red]File {saveTo} already exists. Overwrite file?[/]", false);
                if (!deleteConfirm)
                {
                    AnsiConsole.WriteLine("write aborted");
                    return 0;
                }

                File.Delete(saveTo);
            }

            var data = await resultsFunc();

            switch (ext)
            {
                case "json":
                    var jArray = JArray.FromObject(data);
                    await File.WriteAllTextAsync(saveTo, jArray.ToString(Formatting.Indented));
                    break;


                case "csv":

                    break;

                case "xlsx":
                    SaveToExcel(data, saveTo, props);

                    break;

                default:
                    AnsiConsole.WriteLine("write aborted");
                    break;
            }

            AnsiConsole.WriteLine("File saved to {0}", saveTo);
            if (!dontOpen) Process.Start("explorer.exe", saveTo);
            AnsiConsole.WriteLine();
            return 1;
        }

        return null;
    }

    public static async Task<List<EntityTypeCount>> RetrieveEntityInfoAsync(IServiceProvider serviceProvider, string? where = null)
    {
        var adminApi = serviceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<GetDataHubEntityTypeCountsResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetDataHubEntityTypeCountsRequest),
            Data = JsonConvert.SerializeObject(new GetDataHubEntityTypeCountsRequest()
            {
                WhereClause = where
            })
        });

        return response.Results;
    }


    public static async Task<TRes> RetrievePagedResultsAsync<TReq, TRes>(IServiceProvider serviceProvider, TReq req)
    {
        var adminApi = serviceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<TRes>(new SerializedRequest()
        {
            RequestType = typeof(TReq).Name,
            Data = JsonConvert.SerializeObject(req)
        });

        return response;
    }


    public static async Task<List<TData>> RetrieveAllResultsAsync<TReq, TRes, TData>(IServiceProvider serviceProvider, TReq req, Func<TRes, List<TData>> resultsFunc, Func<TRes, bool> moreResultsFunc, Action<TReq, TRes>? continuationTokenFunc = null)
    {
        var cliApi = serviceProvider.GetRequiredService<ICLIApi>();
        var results = new List<TData>();

        var response = await cliApi.PostAdminMessage<TRes>(new SerializedRequest()
        {
            RequestType = typeof(TReq).Name,
            Data = JsonConvert.SerializeObject(req)
        });
        results.AddRange(resultsFunc(response));

        while (moreResultsFunc(response))
        {
            continuationTokenFunc?.Invoke(req, response);

            response = await cliApi.PostAdminMessage<TRes>(new SerializedRequest()
            {
                RequestType = typeof(TReq).Name,
                Data = JsonConvert.SerializeObject(req)
            });
            var newResults = resultsFunc(response);
            results.AddRange(newResults);
        }

        return results;
    }

}
