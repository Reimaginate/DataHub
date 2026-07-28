using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Commands.Active;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

internal sealed class EntitiesRevertCommand : Command
{
    private readonly ICLIApi _api;
    private readonly Argument<string?> _entityTypeArgument = new("entity-type")
    {
        Description = "DataHub entity type to revert.",
        Arity = ArgumentArity.ZeroOrOne
    };
    private readonly Argument<string[]> _entityIdsArgument = new("entity-id")
    {
        Description = "DataHub entity ids to revert.",
        Arity = ArgumentArity.ZeroOrMore
    };
    private readonly Option<string?> _atOption = new("--at")
    {
        Description = "Point in time to revert to."
    };
    private readonly Option<string?> _trackingEntryIdOption = new("--tracking-entry-id")
    {
        Description = "Tracking entry id to revert to."
    };
    private readonly Option<bool> _silentNotificationsOption = new("--silent-notifications")
    {
        Description = "Do not dispatch update notifications to agents."
    };
    private readonly Option<bool> _dryRunOption = new("--dry-run")
    {
        Description = "Preview the revert without writing tracking or entity data."
    };
    private readonly Option<bool> _yesOption = new("--yes")
    {
        Description = "Skip confirmation prompt."
    };
    private readonly Option<string?> _outputOption = new("--output")
    {
        Description = "Output format: table, json, ndjson, or tsv."
    };

    public EntitiesRevertCommand(IServiceProvider serviceProvider) : base("revert", "Revert DataHub entities to a tracked point in time.")
    {
        _api = serviceProvider.GetRequiredService<ICLIApi>();

        CommandContextOptions.AddTargetOptions(this);
        Add(_outputOption);
        Add(_entityTypeArgument);
        Add(_entityIdsArgument);
        Add(_atOption);
        Add(_trackingEntryIdOption);
        Add(_silentNotificationsOption);
        Add(_dryRunOption);
        Add(_yesOption);

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
            var entityType = parseResult.GetValue(_entityTypeArgument);
            var entityIds = parseResult.GetValue(_entityIdsArgument) ?? [];
            var at = parseResult.GetValue(_atOption);
            var trackingEntryId = parseResult.GetValue(_trackingEntryIdOption);
            var dryRun = parseResult.GetValue(_dryRunOption);
            var yes = parseResult.GetValue(_yesOption);
            var silentNotifications = parseResult.GetValue(_silentNotificationsOption);

            var validation = ValidateTargetOptions(entityType, entityIds, at, trackingEntryId, out var revertTo);
            if (validation != null)
            {
                Console.Error.WriteLine(validation);
                return CliExitCodes.Usage;
            }

            if (!dryRun && !yes)
            {
                var targetDescription = !string.IsNullOrWhiteSpace(trackingEntryId)
                    ? $"tracking entry '{trackingEntryId}'"
                    : $"{entityIds.Length} {entityType} entity/entities";
                if (!AnsiConsole.Confirm($"Revert {targetDescription}?"))
                {
                    return CliExitCodes.Cancelled;
                }
            }

            var response = await Post<RevertDataHubEntitiesResponse>(new RevertDataHubEntitiesRequest
            {
                EntityType = entityType,
                EntityIds = entityIds.ToList(),
                RevertTo = revertTo,
                TrackingEntryId = trackingEntryId,
                DispatchNotifications = !silentNotifications,
                DryRun = dryRun
            }, cancellationToken);

            if (dryRun && CliOutputContext.Format == CliOutputFormat.Table)
            {
                AnsiConsole.MarkupLine("[yellow]Dry run only. No tracking or entity data was written.[/]");
            }

            WriteResults(response.Results);
            return response.Results.Any(result => !result.Success) ? CliExitCodes.Failure : CliExitCodes.Success;
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

    private static string? ValidateTargetOptions(
        string? entityType,
        string[] entityIds,
        string? at,
        string? trackingEntryId,
        out DateTimeOffset? revertTo)
    {
        revertTo = null;
        var hasTrackingEntryId = !string.IsNullOrWhiteSpace(trackingEntryId);
        var hasPointInTimeTarget = !string.IsNullOrWhiteSpace(entityType) || entityIds.Length > 0 || !string.IsNullOrWhiteSpace(at);

        if (hasTrackingEntryId == hasPointInTimeTarget)
        {
            return "Specify exactly one target mode: <entity-type> <entity-id...> --at <date-time>, or --tracking-entry-id <id>.";
        }

        if (hasTrackingEntryId)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            return "Entity type is required when using --at.";
        }

        if (entityIds.Length == 0)
        {
            return "At least one entity id is required when using --at.";
        }

        if (string.IsNullOrWhiteSpace(at))
        {
            return "--at is required when reverting by entity id.";
        }

        if (!DateTimeOffset.TryParse(at, out var parsed))
        {
            return $"Could not parse --at value '{at}' as a date/time.";
        }

        revertTo = parsed;
        return null;
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

    private static void WriteResults(List<RevertDataHubEntityResult> results)
    {
        ConsoleHelper.PrintTable(results, [
            nameof(RevertDataHubEntityResult.EntityType),
            nameof(RevertDataHubEntityResult.EntityId),
            nameof(RevertDataHubEntityResult.Success),
            nameof(RevertDataHubEntityResult.Changed),
            nameof(RevertDataHubEntityResult.RevertedTo),
            nameof(RevertDataHubEntityResult.TrackingEntryId),
            nameof(RevertDataHubEntityResult.FailureReason)
        ]);
    }

}
