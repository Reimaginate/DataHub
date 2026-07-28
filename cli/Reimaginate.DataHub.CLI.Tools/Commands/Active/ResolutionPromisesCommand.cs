using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using DataHubPatch = Reimaginate.DataHub.SharedModels.Core.Patch;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

public sealed class ResolutionPromisesCommand : DataHubTopLevelCommand
{
    private const int DeleteWherePageSize = 500;
    private const string DeletedStatus = "Deleted";
    private const string AlreadyDeletedReason = "Already deleted.";
    private readonly ICLIApi _api;

    public ResolutionPromisesCommand(IServiceProvider serviceProvider) : base("resolution-promises", serviceProvider)
    {
        Description = "Inspect, delete, and resolve deferred entity-reference promises.";
        _api = serviceProvider.GetRequiredService<ICLIApi>();

        Add(ListCommand());
        Add(GetCommand());
        Add(PatchCommand());
        Add(DeleteCommand());
        Add(ResolveCommand());
    }

    private Command ListCommand()
    {
        var command = ActiveCommandFactory.NewCommand("list", "List resolution promises.", out var output);
        var where = ActiveCommandFactory.OptionalArgument("where", "Optional resolution promise query filter.");
        var pageSize = ActiveCommandFactory.IntOption("--page-size", "Page size.", "--page");
        var properties = PropertiesOption();
        var additional = AdditionalPropertiesOption();
        ActiveCommandFactory.Add(command, where, pageSize, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, async () =>
        {
            var response = await Post<ListResolutionPromisesResponse>(new ListResolutionPromisesRequest
            {
                WhereClause = parse.GetValue(where),
                PageSize = parse.GetValue(pageSize) ?? 100
            }, ct);

            ConsoleHelper.PrintTable(response.Results, PromiseColumns(parse.GetValue(properties), parse.GetValue(additional)));
            return CliExitCodes.LegacyRuntimeSuccess;
        }));

        return command;
    }

    private Command GetCommand()
    {
        var command = ActiveCommandFactory.NewCommand("get", "Get resolution promises by id.", out var output);
        var ids = ActiveCommandFactory.RequiredManyArgument("ids", "Resolution promise ids.");
        var properties = PropertiesOption();
        var additional = AdditionalPropertiesOption();
        ActiveCommandFactory.Add(command, ids, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, async () =>
        {
            var response = await Post<GetResolutionPromisesResponse>(new GetResolutionPromisesRequest
            {
                PromiseIds = (parse.GetValue(ids) ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            }, ct);

            ConsoleHelper.PrintTable(response.Results, PromiseColumns(parse.GetValue(properties), parse.GetValue(additional)));
            return CliExitCodes.LegacyRuntimeSuccess;
        }));

        return command;
    }

    private Command DeleteCommand()
    {
        var command = ActiveCommandFactory.NewCommand("delete", "Delete selected resolution promises.", out var output);
        var ids = IdsOption();
        var where = new Option<string?>("--where") { Description = "Resolution promise query filter." };
        var dryRun = ActiveCommandFactory.BoolOption("--dry-run", "Preview matching promises without deleting them.");
        var yes = ActiveCommandFactory.BoolOption("--yes", "Confirm deletion without prompting.");
        var properties = PropertiesOption();
        var additional = AdditionalPropertiesOption();
        ActiveCommandFactory.Add(command, ids, where, dryRun, yes, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, async () =>
        {
            var promiseIds = parse.GetValue(ids) ?? [];
            var whereClause = parse.GetValue(where);
            var isDryRun = parse.GetValue(dryRun);
            var skipConfirmation = parse.GetValue(yes);
            var validation = ValidateTargetOptions(promiseIds, whereClause);
            if (validation != null)
            {
                Console.Error.WriteLine(validation);
                return CliExitCodes.Usage;
            }

            if (!isDryRun && !skipConfirmation && !ConfirmDestructiveOperation("delete selected resolution promises"))
            {
                return CliExitCodes.Cancelled;
            }

            var request = new DeleteResolutionPromisesRequest
            {
                PromiseIds = promiseIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                WhereClause = whereClause,
                PageSize = string.IsNullOrWhiteSpace(whereClause) ? 1000 : DeleteWherePageSize,
                DryRun = isDryRun
            };

            var response = await SendDeleteRequest(request, ct);

            WriteDeleteSummary(response, isDryRun);
            var columns = DeleteColumns(parse.GetValue(properties), parse.GetValue(additional));
            if (CliOutputContext.Format != CliOutputFormat.Table || isDryRun)
            {
                ConsoleHelper.PrintTable(response.Results, columns);
            }
            else
            {
                BulkOperationDetailsPrompt.Show(
                    response.Results.Where(result => IsFailedStatus(result.Status)).ToList(),
                    columns,
                    "resolution-promises-delete-errors",
                    "failed delete result(s)");
            }

            return CliExitCodes.LegacyRuntimeSuccess;
        }));

        return command;
    }

    private Command PatchCommand()
    {
        var command = ActiveCommandFactory.NewCommand("patch", "Patch one resolution promise.", out var output);
        var id = ActiveCommandFactory.RequiredArgument("id", "Resolution promise id.");
        var property = ActiveCommandFactory.RequiredArgument("property", "Property path to set.");
        var value = ActiveCommandFactory.RequiredArgument("value", "Value to set. Parsed as JSON when valid; otherwise treated as a string.");
        var dryRun = ActiveCommandFactory.BoolOption("--dry-run", "Preview the patch without saving it.");
        var yes = ActiveCommandFactory.BoolOption("--yes", "Confirm patching without prompting.");
        var properties = PropertiesOption();
        var additional = AdditionalPropertiesOption();
        ActiveCommandFactory.Add(command, id, property, value, dryRun, yes, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, async () =>
        {
            var promiseId = parse.GetValue(id);
            var propertyPath = parse.GetValue(property);
            var rawValue = parse.GetValue(value);
            var isDryRun = parse.GetValue(dryRun);

            if (string.IsNullOrWhiteSpace(promiseId) || string.IsNullOrWhiteSpace(propertyPath))
            {
                Console.Error.WriteLine("Promise id and property are required.");
                return CliExitCodes.Usage;
            }

            if (!isDryRun && !parse.GetValue(yes) && !ConfirmDestructiveOperation("patch one resolution promise"))
            {
                return CliExitCodes.Cancelled;
            }

            var request = new PatchResolutionPromiseRequest
            {
                PromiseId = promiseId,
                DryRun = isDryRun,
                Operations =
                [
                    new DataHubPatch
                    {
                        Operation = "set",
                        Path = propertyPath,
                        Value = ParseValue(rawValue!)
                    }
                ]
            };

            var response = await SendPatchRequest(request, ct);

            WritePatchSummary(response);
            ConsoleHelper.PrintTable(new List<PatchResolutionPromiseResponse> { response }, PatchColumns(parse.GetValue(properties), parse.GetValue(additional)));
            return response.Success ? CliExitCodes.LegacyRuntimeSuccess : CliExitCodes.LegacyRuntimeFailure;
        }));

        return command;
    }

    private Command ResolveCommand()
    {
        var command = ActiveCommandFactory.NewCommand("resolve", "Resolve selected resolution promises.", out var output);
        var ids = IdsOption();
        var where = new Option<string?>("--where") { Description = "Resolution promise query filter." };
        var dryRun = ActiveCommandFactory.BoolOption("--dry-run", "Preview matching promises without applying updates or deleting promises.");
        var doNotTrack = ActiveCommandFactory.BoolOption("--do-not-track", "Resolve references without writing DataHub change tracking entries.");
        var stopOnFailure = ActiveCommandFactory.BoolOption("--stop-on-failure", "Stop resolving when a recoverable per-promise failure is encountered.");
        var yes = ActiveCommandFactory.BoolOption("--yes", "Confirm resolution without prompting.");
        var properties = PropertiesOption();
        var additional = AdditionalPropertiesOption();
        ActiveCommandFactory.Add(command, ids, where, dryRun, doNotTrack, stopOnFailure, yes, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, async () =>
        {
            var promiseIds = parse.GetValue(ids) ?? [];
            var whereClause = parse.GetValue(where);
            var isDryRun = parse.GetValue(dryRun);
            var skipConfirmation = parse.GetValue(yes);
            var validation = ValidateTargetOptions(promiseIds, whereClause);
            if (validation != null)
            {
                Console.Error.WriteLine(validation);
                return CliExitCodes.Usage;
            }

            if (!isDryRun && !skipConfirmation && !ConfirmDestructiveOperation("resolve selected resolution promises"))
            {
                return CliExitCodes.Cancelled;
            }

            var request = new ResolveResolutionPromisesRequest
            {
                PromiseIds = promiseIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                WhereClause = whereClause,
                DryRun = isDryRun,
                DoNotTrack = parse.GetValue(doNotTrack),
                StopOnFailure = parse.GetValue(stopOnFailure)
            };

            var response = await SendResolveRequest(request, ct);

            WriteResolveSummary(response, isDryRun);
            var columns = ResolveColumns(parse.GetValue(properties), parse.GetValue(additional));
            if (CliOutputContext.Format != CliOutputFormat.Table || isDryRun)
            {
                ConsoleHelper.PrintTable(response.Results, columns);
            }
            else
            {
                BulkOperationDetailsPrompt.Show(
                    response.Results.Where(result => IsFailedStatus(result.Status)).ToList(),
                    columns,
                    "resolution-promises-resolve-errors",
                    "failed resolve result(s)");
            }

            return CliExitCodes.LegacyRuntimeSuccess;
        }));

        return command;
    }

    private static Option<string[]> IdsOption()
    {
        return new Option<string[]>("--ids")
        {
            Description = "Resolution promise ids.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
    }

    private static Option<string?> PropertiesOption()
        => ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");

    private static Option<string?> AdditionalPropertiesOption()
        => ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");

    private async Task<T> Post<T>(DataHubCLIRequest request, CancellationToken cancellationToken)
    {
        request.CorrelationId ??= Guid.NewGuid().ToString("N");
        request.RequestType ??= request.GetType().Name;

        return await _api.PostAdminMessage<T>(new SerializedRequest
        {
            RequestType = request.RequestType,
            CorrelationId = request.CorrelationId,
            Data = JsonConvert.SerializeObject(request)
        }, cancellationToken);
    }

    private async Task<PatchResolutionPromiseResponse> SendPatchRequest(PatchResolutionPromiseRequest request, CancellationToken cancellationToken)
    {
        return await CliProgressHelper.RunAsync(async progress =>
        {
            var operation = request.DryRun ? "Scanning resolution promise patch" : "Patching resolution promise";
            var progressTask = progress.AddTask($"{operation}: {request.PromiseId}", maxValue: 1);
            progressTask.StartTask();

            var response = await Post<PatchResolutionPromiseResponse>(request, cancellationToken);
            progressTask.Description = $"{operation}: {request.PromiseId}, status {response.Status ?? "Unknown"}, changed {response.Changed.ToString().ToLowerInvariant()}";
            progressTask.Value = 1;
            progressTask.StopTask();
            return response;
        });
    }

    private async Task<DeleteResolutionPromisesResponse> SendDeleteRequest(DeleteResolutionPromisesRequest request, CancellationToken cancellationToken)
    {
        if (request.PromiseIds.Count > 0)
        {
            return await Post<DeleteResolutionPromisesResponse>(request, cancellationToken);
        }

        return await CliProgressHelper.RunAsync(async progress =>
        {
            var aggregate = new DeleteResolutionPromisesResponse();
            var progressTask = StartWhereProgress(progress, request.DryRun ? "Scanning matching promises" : "Deleting matching promises");
            var pageNumber = 0;
            var stoppedBeforeAllResults = false;
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pageNumber++;
                    progressTask.Description = $"{DeleteWhereOperation(request.DryRun)}: loading {DeleteWhereUnit(request.DryRun)} {pageNumber}, matched {aggregate.MatchedCount}, deleted {aggregate.DeletedCount}, failed {aggregate.FailedCount}";

                    var page = await Post<DeleteResolutionPromisesResponse>(request, cancellationToken);
                    aggregate.MatchedCount += page.MatchedCount;
                    aggregate.DeletedCount += page.DeletedCount;
                    aggregate.FailedCount += page.FailedCount;
                    aggregate.Results.AddRange(page.Results ?? []);
                    request.ContinuationToken = NextDeleteWhereContinuationToken(request, page);
                    aggregate.ContinuationToken = page.ContinuationToken;
                    aggregate.MoreResultsAvailable = page.MoreResultsAvailable;
                    UpdateDeleteWhereProgress(progressTask, request.DryRun, pageNumber, aggregate, page.Results?.Select(result => result.PromiseId));
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!ShouldContinueDeleteWhere(request, page))
                    {
                        stoppedBeforeAllResults = page.MoreResultsAvailable;
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                CancelWhereProgress(progressTask, DeleteWhereOperation(request.DryRun), pageNumber, aggregate.MatchedCount);
                throw;
            }

            CompleteWhereProgress(progressTask);
            aggregate.MoreResultsAvailable = stoppedBeforeAllResults;
            aggregate.ContinuationToken = stoppedBeforeAllResults && request.DryRun ? request.ContinuationToken : null;
            return aggregate;
        });
    }

    private async Task<ResolveResolutionPromisesResponse> SendResolveRequest(ResolveResolutionPromisesRequest request, CancellationToken cancellationToken)
    {
        if (request.PromiseIds.Count > 0)
        {
            return await Post<ResolveResolutionPromisesResponse>(request, cancellationToken);
        }

        return await CliProgressHelper.RunAsync(async progress =>
        {
            var aggregate = new ResolveResolutionPromisesResponse();
            var progressTask = StartWhereProgress(progress, request.DryRun ? "Scanning matching promises" : "Resolving matching promises");
            var pageNumber = 0;
            try
            {
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pageNumber++;
                    progressTask.Description = $"{ResolveWhereOperation(request.DryRun)}: loading page {pageNumber}, matched {aggregate.MatchedCount}, resolved {aggregate.ResolvedCount}, unresolved {aggregate.UnresolvedCount}, stale {aggregate.DeletedStaleCount}, failed {aggregate.FailedCount}";

                    var page = await Post<ResolveResolutionPromisesResponse>(request, cancellationToken);
                    aggregate.MatchedCount += page.MatchedCount;
                    aggregate.ResolvedCount += page.ResolvedCount;
                    aggregate.UnresolvedCount += page.UnresolvedCount;
                    aggregate.DeletedStaleCount += page.DeletedStaleCount;
                    aggregate.FailedCount += page.FailedCount;
                    aggregate.Results.AddRange(page.Results ?? []);
                    request.ContinuationToken = page.MoreResultsAvailable ? page.ContinuationToken : null;
                    aggregate.ContinuationToken = page.ContinuationToken;
                    aggregate.MoreResultsAvailable = page.MoreResultsAvailable;
                    UpdateResolveWhereProgress(progressTask, request.DryRun, pageNumber, aggregate, page.Results?.Select(result => result.PromiseId));
                    cancellationToken.ThrowIfCancellationRequested();
                } while (!string.IsNullOrWhiteSpace(request.ContinuationToken));
            }
            catch (OperationCanceledException)
            {
                CancelWhereProgress(progressTask, ResolveWhereOperation(request.DryRun), pageNumber, aggregate.MatchedCount);
                throw;
            }

            CompleteWhereProgress(progressTask);
            aggregate.MoreResultsAvailable = false;
            aggregate.ContinuationToken = null;
            return aggregate;
        });
    }

    private static CliProgressTask StartWhereProgress(CliProgressContext progress, string operation)
    {
        var task = progress.AddTask($"{operation}: starting", maxValue: 1);
        task.IsIndeterminate = true;
        task.StartTask();
        return task;
    }

    private static void UpdateDeleteWhereProgress(CliProgressTask task, bool dryRun, int pageNumber, DeleteResolutionPromisesResponse aggregate, IEnumerable<string>? promiseIds)
    {
        var action = dryRun ? "would delete" : "deleted";
        var affectedCount = dryRun ? aggregate.MatchedCount : aggregate.DeletedCount;
        task.Description = $"{DeleteWhereOperation(dryRun)}: {DeleteWhereUnit(dryRun)} {pageNumber}, matched {aggregate.MatchedCount}, {action} {affectedCount}, failed {aggregate.FailedCount}{LatestPromiseIds(promiseIds)}";
        task.IsIndeterminate = true;
    }

    private static string? NextDeleteWhereContinuationToken(DeleteResolutionPromisesRequest request, DeleteResolutionPromisesResponse page)
    {
        if (!page.MoreResultsAvailable)
        {
            return null;
        }

        return request.DryRun ? page.ContinuationToken : null;
    }

    private static bool ShouldContinueDeleteWhere(DeleteResolutionPromisesRequest request, DeleteResolutionPromisesResponse page)
    {
        if (!page.MoreResultsAvailable)
        {
            return false;
        }

        if (request.DryRun)
        {
            return !string.IsNullOrWhiteSpace(request.ContinuationToken);
        }

        return DeleteWhereMadeProgress(page) && page.FailedCount == 0;
    }

    private static bool DeleteWhereMadeProgress(DeleteResolutionPromisesResponse page)
    {
        if (page.Results is { Count: > 0 })
        {
            return page.Results.Any(result =>
                string.Equals(result.Status, DeletedStatus, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(result.Reason, AlreadyDeletedReason, StringComparison.OrdinalIgnoreCase));
        }

        return page.DeletedCount > 0;
    }

    private static void UpdateResolveWhereProgress(CliProgressTask task, bool dryRun, int pageNumber, ResolveResolutionPromisesResponse aggregate, IEnumerable<string>? promiseIds)
    {
        var action = dryRun ? "would resolve" : "resolved";
        task.Description = $"{ResolveWhereOperation(dryRun)}: page {pageNumber}, matched {aggregate.MatchedCount}, {action} {aggregate.ResolvedCount}, unresolved {aggregate.UnresolvedCount}, stale {aggregate.DeletedStaleCount}, failed {aggregate.FailedCount}{LatestPromiseIds(promiseIds)}";
        task.IsIndeterminate = true;
    }

    private static string DeleteWhereOperation(bool dryRun)
        => dryRun ? "Scanning matching promises" : "Deleting matching promises";

    private static string DeleteWhereUnit(bool dryRun)
        => dryRun ? "page" : "batch";

    private static string ResolveWhereOperation(bool dryRun)
        => dryRun ? "Scanning matching promises" : "Resolving matching promises";

    private static string LatestPromiseIds(IEnumerable<string>? promiseIds)
    {
        var latestIds = string.Join(", ", (promiseIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Take(3));
        return string.IsNullOrWhiteSpace(latestIds) ? "" : $", latest {latestIds}";
    }

    private static void CompleteWhereProgress(CliProgressTask task)
    {
        task.IsIndeterminate = false;
        task.MaxValue = 1;
        task.Value = task.MaxValue;
        task.StopTask();
    }

    private static void CancelWhereProgress(CliProgressTask task, string operation, int pageNumber, int matchedCount)
    {
        task.Description = $"{operation}: cancelled after page {pageNumber}, matched {matchedCount}";
        task.IsIndeterminate = false;
        task.MaxValue = 1;
        task.Value = 1;
        task.StopTask();
    }

    private static string? ValidateTargetOptions(string[] ids, string? where)
    {
        var hasIds = ids.Any(id => !string.IsNullOrWhiteSpace(id));
        var hasWhere = !string.IsNullOrWhiteSpace(where);
        if (hasIds == hasWhere)
        {
            return "Specify exactly one target mode: --ids or --where.";
        }

        return null;
    }

    private static bool ConfirmDestructiveOperation(string operation)
    {
        Console.Error.WriteLine($"WARNING: This will {operation}. Type Y to continue, or anything else to cancel.");
        Console.Error.Write("> ");
        return string.Equals(Console.ReadLine(), "Y", StringComparison.OrdinalIgnoreCase);
    }

    private static JToken ParseValue(string value)
    {
        try
        {
            return JToken.Parse(value);
        }
        catch (JsonReaderException)
        {
            return JValue.CreateString(value);
        }
    }

    private static List<string> PromiseColumns(string? properties = null, string? additionalProperties = null)
        => DisplayColumns(DefaultPromiseColumns(), properties, additionalProperties);

    private static List<string> DefaultPromiseColumns()
    {
        return
        [
            nameof(ResolutionPromiseResult.PromiseId),
            nameof(ResolutionPromiseResult.DataHubEntityType),
            nameof(ResolutionPromiseResult.DataHubEntityId),
            nameof(ResolutionPromiseResult.EntityReferencePath),
            nameof(ResolutionPromiseResult.DataSource),
            nameof(ResolutionPromiseResult.SourceEntityType),
            nameof(ResolutionPromiseResult.SourceEntityId),
            nameof(ResolutionPromiseResult.TargetEntityType)
        ];
    }

    private static List<string> DeleteColumns(string? properties = null, string? additionalProperties = null)
        => DisplayColumns(DefaultDeleteColumns(), properties, additionalProperties);

    private static List<string> DefaultDeleteColumns()
    {
        return
        [
            nameof(DeleteResolutionPromiseResult.PromiseId),
            nameof(DeleteResolutionPromiseResult.Status),
            nameof(DeleteResolutionPromiseResult.Reason),
            nameof(DeleteResolutionPromiseResult.DataHubEntityType),
            nameof(DeleteResolutionPromiseResult.DataHubEntityId),
            nameof(DeleteResolutionPromiseResult.EntityReferencePath),
            nameof(DeleteResolutionPromiseResult.DataSource),
            nameof(DeleteResolutionPromiseResult.SourceEntityType),
            nameof(DeleteResolutionPromiseResult.SourceEntityId),
            nameof(DeleteResolutionPromiseResult.TargetEntityType)
        ];
    }

    private static List<string> PatchColumns(string? properties = null, string? additionalProperties = null)
        => DisplayColumns(DefaultPatchColumns(), properties, additionalProperties);

    private static List<string> DefaultPatchColumns()
    {
        return
        [
            nameof(PatchResolutionPromiseResponse.PromiseId),
            nameof(PatchResolutionPromiseResponse.Status),
            nameof(PatchResolutionPromiseResponse.Reason),
            nameof(PatchResolutionPromiseResponse.Changed),
            $"{nameof(PatchResolutionPromiseResponse.Result)}.{nameof(ResolutionPromiseResult.DataHubEntityType)}",
            $"{nameof(PatchResolutionPromiseResponse.Result)}.{nameof(ResolutionPromiseResult.DataHubEntityId)}",
            $"{nameof(PatchResolutionPromiseResponse.Result)}.{nameof(ResolutionPromiseResult.EntityReferencePath)}",
            $"{nameof(PatchResolutionPromiseResponse.Result)}.{nameof(ResolutionPromiseResult.DataSource)}",
            $"{nameof(PatchResolutionPromiseResponse.Result)}.{nameof(ResolutionPromiseResult.SourceEntityType)}",
            $"{nameof(PatchResolutionPromiseResponse.Result)}.{nameof(ResolutionPromiseResult.SourceEntityId)}",
            $"{nameof(PatchResolutionPromiseResponse.Result)}.{nameof(ResolutionPromiseResult.TargetEntityType)}"
        ];
    }

    private static List<string> ResolveColumns(string? properties = null, string? additionalProperties = null)
        => DisplayColumns(DefaultResolveColumns(), properties, additionalProperties);

    private static List<string> DefaultResolveColumns()
    {
        return
        [
            nameof(ResolveResolutionPromiseResult.PromiseId),
            nameof(ResolveResolutionPromiseResult.Status),
            nameof(ResolveResolutionPromiseResult.Reason),
            nameof(ResolveResolutionPromiseResult.DataHubEntityType),
            nameof(ResolveResolutionPromiseResult.DataHubEntityId),
            nameof(ResolveResolutionPromiseResult.EntityReferencePath),
            nameof(ResolveResolutionPromiseResult.TargetEntityType),
            nameof(ResolveResolutionPromiseResult.ResolvedEntityId)
        ];
    }

    private static List<string> DisplayColumns(List<string> defaultColumns, string? properties = null, string? additionalProperties = null)
    {
        if (CliOutputContext.Format != CliOutputFormat.Table)
        {
            return defaultColumns;
        }

        var columns = (properties ?? string.Join(",", defaultColumns))
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(property => property.Trim())
            .ToList();

        if (!string.IsNullOrWhiteSpace(additionalProperties))
        {
            columns.AddRange(additionalProperties.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(property => property.Trim()));
        }

        return columns;
    }

    private static bool IsFailedStatus(string? status)
    {
        return string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteDeleteSummary(DeleteResolutionPromisesResponse response, bool dryRun)
    {
        if (CliOutputContext.Format != CliOutputFormat.Table)
        {
            return;
        }

        var prefix = dryRun ? "Dry run: " : "";
        Console.WriteLine($"{prefix}matched {response.MatchedCount}, deleted {response.DeletedCount}, failed {response.FailedCount}.");
    }

    private static void WritePatchSummary(PatchResolutionPromiseResponse response)
    {
        if (CliOutputContext.Format != CliOutputFormat.Table)
        {
            return;
        }

        Console.WriteLine($"{response.Status}: promise {response.PromiseId}, changed {response.Changed.ToString().ToLowerInvariant()}.");
    }

    private static void WriteResolveSummary(ResolveResolutionPromisesResponse response, bool dryRun)
    {
        if (CliOutputContext.Format != CliOutputFormat.Table)
        {
            return;
        }

        var prefix = dryRun ? "Dry run: " : "";
        Console.WriteLine($"{prefix}matched {response.MatchedCount}, resolved {response.ResolvedCount}, unresolved {response.UnresolvedCount}, stale {response.DeletedStaleCount}, failed {response.FailedCount}.");
    }
}
