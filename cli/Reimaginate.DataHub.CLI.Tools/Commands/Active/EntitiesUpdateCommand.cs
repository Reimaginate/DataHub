using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Commands.Active;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;
using DataHubPatch = Reimaginate.DataHub.SharedModels.Core.Patch;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

internal sealed class EntitiesUpdateCommand : Command
{
    private const int QueryPageSize = 1000;
    private const int PatchBatchSize = 1000;

    private readonly ICLIApi _api;
    private readonly Argument<string> _propertyArgument = new("property")
    {
        Description = "Property path to update."
    };
    private readonly Argument<string> _valueArgument = new("value")
    {
        Description = "Value to set. Parsed as JSON when valid; otherwise treated as a string."
    };
    private readonly Option<string?> _whereOption = new("--where")
    {
        Description = "DataHub query selecting entities to update."
    };
    private readonly Option<string?> _entityTypeOption = new("--entity-type")
    {
        Description = "Entity type for explicit id updates."
    };
    private readonly Option<string[]> _idsOption = new("--ids")
    {
        Description = "Entity ids to update.",
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true
    };
    private readonly Option<bool> _yesOption = new("--yes")
    {
        Description = "Skip confirmation prompt."
    };
    private readonly Option<bool> _noUpdateTimestampOption = new("--no-update-timestamp")
    {
        Description = "Patch without updating last updated timestamps."
    };
    private readonly Option<bool> _notifyAgentsOption = new("--notify-agents")
    {
        Description = "Dispatch notifications to agents."
    };
    private readonly Option<bool> _noNotificationsOption = new("--no-notifications")
    {
        Description = "Do not dispatch update notifications to agents."
    };
    private readonly Option<bool> _continueOption = new("--continue")
    {
        Description = "Continue on failure."
    };
    private readonly Option<bool> _dryRunOption = new("--dry-run")
    {
        Description = "Preview matched entities and proposed values without applying updates."
    };
    private readonly Option<string?> _outputOption = new("--output")
    {
        Description = "Output format: table, json, ndjson, or tsv."
    };

    public EntitiesUpdateCommand(IServiceProvider serviceProvider) : base("update", "Update one property across DataHub entities.")
    {
        _api = serviceProvider.GetRequiredService<ICLIApi>();

        CommandContextOptions.AddTargetOptions(this);
        _noUpdateTimestampOption.Aliases.Add("--silent");
        _noNotificationsOption.Aliases.Add("--silent-notifications");

        Add(_outputOption);
        Add(_propertyArgument);
        Add(_valueArgument);
        Add(_whereOption);
        Add(_entityTypeOption);
        Add(_idsOption);
        Add(_yesOption);
        Add(_noUpdateTimestampOption);
        Add(_notifyAgentsOption);
        Add(_noNotificationsOption);
        Add(_continueOption);
        Add(_dryRunOption);

        SetAction(InvokeAsync);
    }

    private async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var previousTarget = CliTargetContext.Current;
        var previousOutputFormat = CliOutputContext.Format;
        CliTargetContext.Current = CommandContextOptions.CreateTargetOptions(parseResult);
        CliOutputContext.Format = CommandContextOptions.ParseOutputFormat(parseResult.GetValue(_outputOption));

        try
        {
            var property = parseResult.GetValue(_propertyArgument)!;
            var value = parseResult.GetValue(_valueArgument)!;
            var where = parseResult.GetValue(_whereOption);
            var entityType = parseResult.GetValue(_entityTypeOption);
            var ids = parseResult.GetValue(_idsOption) ?? [];
            var yes = parseResult.GetValue(_yesOption);
            var silent = parseResult.GetValue(_noUpdateTimestampOption);
            var notifyAgents = !parseResult.GetValue(_noNotificationsOption);
            var shouldContinue = parseResult.GetValue(_continueOption);
            var dryRun = parseResult.GetValue(_dryRunOption);

            var validation = ValidateTargetOptions(property, where, entityType, ids);
            if (validation != null)
            {
                Console.Error.WriteLine(validation);
                return CliExitCodes.Usage;
            }

            var operation = new DataHubPatch
            {
                Operation = "set",
                Path = property,
                Value = ParseValue(value)
            };

            var targets = !string.IsNullOrWhiteSpace(where)
                ? await FetchTargetsByQuery(where, cancellationToken)
                : await CliProgressHelper.StatusAsync("Loading target entities...", () => FetchTargetsById(entityType!, ids, cancellationToken));

            if (targets.Count == 0)
            {
                WriteStatus("[yellow]No matching entities found.[/]");
                if (dryRun)
                {
                    WritePreviewResults([]);
                }
                else
                {
                    WriteResults([new EntitiesUpdateResult { Matched = 0, Updated = 0, Failed = 0 }]);
                }

                return CliExitCodes.Success;
            }

            if (dryRun)
            {
                WritePreviewResults(CreatePreviewResults(targets, property, operation.Value));
                return CliExitCodes.Success;
            }

            if (!yes && !AnsiConsole.Confirm($"Update property '{property}' on {targets.Count} DataHub entities?"))
            {
                return CliExitCodes.Cancelled;
            }

            var responses = new List<PatchEntityResponse>();
            var failures = new List<PatchEntityResponse>();
            await CliProgressHelper.RunAsync(async progress =>
            {
                var patchTask = progress.AddTask($"Patching {targets.Count} DataHub entities", Math.Max(targets.Count, 1));
                patchTask.StartTask();

                foreach (var batch in targets.Chunk(PatchBatchSize))
                {
                    var batchTargets = batch.ToList();
                    var patchResponses = await SendPatchBatch(batchTargets, operation, silent, notifyAgents, cancellationToken);
                    responses.AddRange(patchResponses);

                    failures.AddRange(patchResponses.Where(response => !response.Success));
                    patchTask.Increment(batchTargets.Count);
                    if (failures.Count != 0 && !shouldContinue)
                    {
                        break;
                    }
                }

                patchTask.Value = patchTask.MaxValue;
                patchTask.StopTask();
            });

            var result = new EntitiesUpdateResult
            {
                Matched = targets.Count,
                Updated = responses.Count(response => response.Success && (response.Changed ?? true)),
                Failed = failures.Count,
                Failures = failures.SelectMany(ToFailureResults).ToList()
            };

            WriteResults([result]);
            return result.Failed == 0 ? CliExitCodes.Success : CliExitCodes.Failure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return CliExitCodes.Failure;
        }
        finally
        {
            CliTargetContext.Current = previousTarget;
            CliOutputContext.Format = previousOutputFormat;
        }
    }

    private static string? ValidateTargetOptions(string property, string? where, string? entityType, string[] ids)
    {
        if (string.IsNullOrWhiteSpace(property))
        {
            return "Property is required.";
        }

        var hasWhere = !string.IsNullOrWhiteSpace(where);
        var hasIds = ids.Length > 0;
        if (hasWhere == hasIds)
        {
            return "Specify exactly one target mode: --where, or --ids with --entity-type.";
        }

        if (hasIds && string.IsNullOrWhiteSpace(entityType))
        {
            return "--entity-type is required when --ids is specified.";
        }

        if (hasWhere && !string.IsNullOrWhiteSpace(entityType))
        {
            return "--entity-type can only be used with --ids.";
        }

        return null;
    }

    private async Task<List<EntityTarget>> FetchTargetsByQuery(string where, CancellationToken cancellationToken)
    {
        var targets = new List<EntityTarget>();
        string? continuationToken = null;
        await CliProgressHelper.RunAsync(async progress =>
        {
            var queryTask = progress.AddTask("Matching DataHub entities");
            queryTask.StartTask();

            do
            {
                var response = await Post<GetEntitiesResponse>(new GetEntitiesWhereRequest
                {
                    WhereClause = where,
                    PageSize = QueryPageSize,
                    ContinuationToken = continuationToken,
                    GetTotalResultCount = continuationToken == null
                }, cancellationToken);

                if (continuationToken == null)
                {
                    queryTask.MaxValue = Math.Max(Math.Max(response.ResultCount, response.Results?.Count ?? 0), 1);
                }

                var pageTargets = ToTargets(response.Results);
                targets.AddRange(pageTargets);
                queryTask.Increment(pageTargets.Count);
                continuationToken = response.MoreResultsAvailable ? response.ContinuationToken : null;
            } while (!string.IsNullOrWhiteSpace(continuationToken));

            queryTask.Value = queryTask.MaxValue;
            queryTask.StopTask();
        });

        return targets;
    }

    private async Task<List<EntityTarget>> FetchTargetsById(string entityType, IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        var response = await Post<GetEntitiesResponse>(new GetEntitiesByIdRequest
        {
            EntityType = entityType,
            EntityIds = ids.ToList()
        }, cancellationToken);

        return ToTargets(response.Results, entityType);
    }

    private async Task<List<PatchEntityResponse>> SendPatchBatch(
        IReadOnlyCollection<EntityTarget> targets,
        DataHubPatch operation,
        bool silent,
        bool notifyAgents,
        CancellationToken cancellationToken)
    {
        var request = new PatchEntitiesRequest
        {
            Silent = silent,
            DispatchNotifications = notifyAgents,
            Requests = targets.Select(target => new PatchEntityRequest
            {
                DataSource = DataSources.DataHub,
                EntityType = target.EntityType,
                EntityId = target.Id,
                Operations = [ClonePatch(operation)],
                Silent = silent,
                DispatchNotifications = notifyAgents
            }).ToList()
        };

        return await Post<List<PatchEntityResponse>>(request, cancellationToken);
    }

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

    private static List<EntityTarget> ToTargets(IEnumerable<JObject>? results, string? fallbackEntityType = null)
    {
        return (results ?? [])
            .Select(result => new EntityTarget(
                result.Value<string>(nameof(DataHubEntity.id)) ?? result.Value<string>("Id") ?? string.Empty,
                result.Value<string>(nameof(DataHubEntity.entityType)) ?? fallbackEntityType ?? string.Empty,
                result))
            .Where(target => !string.IsNullOrWhiteSpace(target.Id) && !string.IsNullOrWhiteSpace(target.EntityType))
            .ToList();
    }

    private static DataHubPatch ClonePatch(DataHubPatch operation)
    {
        return new DataHubPatch
        {
            Operation = operation.Operation,
            Path = operation.Path,
            Regex = operation.Regex,
            Value = operation.Value.DeepClone(),
            DispatchNotifications = operation.DispatchNotifications
        };
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

    private static IEnumerable<EntitiesUpdateFailure> ToFailureResults(PatchEntityResponse response)
    {
        if (response.PatchFailures?.Count > 0)
        {
            return response.PatchFailures.Select(failure => new EntitiesUpdateFailure
            {
                DataSource = response.DataSource,
                EntityType = response.EntityType,
                EntityId = response.EntityId,
                FailureReason = failure.FailureReason,
                Patch = failure.Patch
            });
        }

        return
        [
            new EntitiesUpdateFailure
            {
                DataSource = response.DataSource,
                EntityType = response.EntityType,
                EntityId = response.EntityId,
                FailureReason = response.FailureReason
            }
        ];
    }

    private static void WriteStatus(string markup)
    {
        if (CliOutputContext.Format == CliOutputFormat.Table)
        {
            AnsiConsole.MarkupLine(markup);
        }
    }

    private static void WriteResults(List<EntitiesUpdateResult> results)
    {
        if (CliOutputContext.Format != CliOutputFormat.Table)
        {
            ConsoleHelper.PrintTable(results, [nameof(EntitiesUpdateResult.Matched), nameof(EntitiesUpdateResult.Updated), nameof(EntitiesUpdateResult.Failed)]);
            return;
        }

        foreach (var result in results)
        {
            Console.WriteLine($"Matched {result.Matched}, updated {result.Updated}, failed {result.Failed}.");
        }

        BulkOperationDetailsPrompt.Show(
            results.SelectMany(result => result.Failures).ToList(),
            [
                nameof(EntitiesUpdateFailure.DataSource),
                nameof(EntitiesUpdateFailure.EntityType),
                nameof(EntitiesUpdateFailure.EntityId),
                nameof(EntitiesUpdateFailure.FailureReason)
            ],
            "entities-update-errors",
            "failed entity update result(s)");
    }

    private static List<EntitiesUpdatePreviewResult> CreatePreviewResults(List<EntityTarget> targets, string property, JToken newValue)
    {
        return targets.Select(target => new EntitiesUpdatePreviewResult
        {
            Matched = targets.Count,
            EntityType = target.EntityType,
            EntityId = target.Id,
            Property = property,
            CurrentValue = target.Entity.SelectToken(property, false)?.DeepClone() ?? JValue.CreateNull(),
            NewValue = newValue.DeepClone()
        }).ToList();
    }

    private static void WritePreviewResults(List<EntitiesUpdatePreviewResult> results)
    {
        if (CliOutputContext.Format == CliOutputFormat.Table)
        {
            WriteStatus($"[yellow]Dry run matched {results.FirstOrDefault()?.Matched ?? 0} DataHub entities. No updates were applied.[/]");
        }

        ConsoleHelper.PrintTable(results, [
            nameof(EntitiesUpdatePreviewResult.EntityType),
            nameof(EntitiesUpdatePreviewResult.EntityId),
            nameof(EntitiesUpdatePreviewResult.Property),
            nameof(EntitiesUpdatePreviewResult.CurrentValue),
            nameof(EntitiesUpdatePreviewResult.NewValue)
        ]);
    }

    private sealed record EntityTarget(string Id, string EntityType, JObject Entity);

    private sealed class EntitiesUpdateResult
    {
        public int Matched { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        public List<EntitiesUpdateFailure> Failures { get; set; } = [];
    }

    private sealed class EntitiesUpdatePreviewResult
    {
        public int Matched { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? Property { get; set; }
        public JToken? CurrentValue { get; set; }
        public JToken? NewValue { get; set; }
    }

    private sealed class EntitiesUpdateFailure
    {
        public string? DataSource { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? FailureReason { get; set; }
        public DataHubPatch? Patch { get; set; }
    }
}
