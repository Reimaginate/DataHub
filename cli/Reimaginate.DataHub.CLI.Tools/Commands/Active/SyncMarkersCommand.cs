using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

public sealed class SyncMarkersCommand : DataHubTopLevelCommand
{
    private readonly ICLIApi _api;

    public SyncMarkersCommand(IServiceProvider serviceProvider) : base("sync-markers", serviceProvider)
    {
        Description = "Inspect and patch sync markers.";
        _api = serviceProvider.GetRequiredService<ICLIApi>();

        Add(ListCommand());
        Add(GetCommand());
        Add(PatchCommand());
    }

    private Command ListCommand()
    {
        var command = ActiveCommandFactory.NewCommand("list", "List sync markers.", out var output);
        var where = ActiveCommandFactory.OptionalArgument("where", "Optional sync marker query filter.");
        var pageSize = ActiveCommandFactory.IntOption("--page-size", "Page size.", "--page");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        ActiveCommandFactory.Add(command, where, pageSize, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, async () =>
        {
            var response = await Post<GetSyncMarkersResponse>(new GetSyncMarkersRequest
            {
                WhereClause = parse.GetValue(where),
                PageSize = parse.GetValue(pageSize) ?? 100,
                OrderBy = $"x.{nameof(SyncMarker.DataSource)}, x.{nameof(SyncMarker.AgentId)}, x.{nameof(SyncMarker.EntityType)}"
            }, ct);

            ConsoleHelper.PrintTable((response.Results ?? []).ToList(), MarkerColumns(parse.GetValue(properties), parse.GetValue(additional)));
            return CliExitCodes.LegacyRuntimeSuccess;
        }));

        return command;
    }

    private Command GetCommand()
    {
        var command = ActiveCommandFactory.NewCommand("get", "Get a sync marker by id.", out var output);
        var id = ActiveCommandFactory.RequiredArgument("id", "Sync marker id.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        ActiveCommandFactory.Add(command, id, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, async () =>
        {
            var markerId = parse.GetValue(id)!;
            var response = await Post<GetSyncMarkersResponse>(new GetSyncMarkersRequest
            {
                WhereClause = $"x.{nameof(SyncMarker.id)} = @id",
                Parameters = [new DataHubQueryParameter { Name = "id", Value = markerId }],
                PageSize = 1
            }, ct);

            ConsoleHelper.PrintTable((response.Results ?? []).ToList(), MarkerColumns(parse.GetValue(properties), parse.GetValue(additional)));
            return CliExitCodes.LegacyRuntimeSuccess;
        }));

        return command;
    }

    private Command PatchCommand()
    {
        var command = ActiveCommandFactory.NewCommand("patch", "Patch a sync marker by id.", out var output);
        var id = ActiveCommandFactory.RequiredArgument("id", "Sync marker id.");
        var value = ActiveCommandFactory.StringOption("--value", "New literal sync marker value.");
        var lastRunTime = ActiveCommandFactory.StringOption("--last-run-time", "New last-run timestamp, or 'now'.");
        var dryRun = ActiveCommandFactory.BoolOption("--dry-run", "Preview the patch without saving it.");
        var yes = ActiveCommandFactory.BoolOption("--yes", "Confirm patching without prompting.");
        ActiveCommandFactory.Add(command, id, value, lastRunTime, dryRun, yes);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, async () =>
        {
            var markerId = parse.GetValue(id)!;
            var rawValue = parse.GetValue(value);
            var rawLastRunTime = parse.GetValue(lastRunTime);
            var updateValue = rawValue != null;
            var updateLastRunTime = rawLastRunTime != null;
            if (!updateValue && !updateLastRunTime)
            {
                Console.Error.WriteLine("Specify at least one field to patch: --value or --last-run-time.");
                return CliExitCodes.Usage;
            }

            if (!TryParseLastRunTime(rawLastRunTime, out var parsedLastRunTime, out var parseError))
            {
                Console.Error.WriteLine(parseError);
                return CliExitCodes.Usage;
            }

            var isDryRun = parse.GetValue(dryRun);
            if (!isDryRun && !parse.GetValue(yes) && !ConfirmPatch(markerId))
            {
                return CliExitCodes.Cancelled;
            }

            var response = await Post<PatchSyncMarkerResponse>(new PatchSyncMarkerRequest
            {
                MarkerId = markerId,
                Value = rawValue,
                UpdateValue = updateValue,
                LastRunTime = parsedLastRunTime,
                UpdateLastRunTime = updateLastRunTime,
                DryRun = isDryRun
            }, ct);

            WritePatchSummary(response);
            ConsoleHelper.PrintTable(new List<PatchSyncMarkerResponse> { response }, PatchColumns());
            return response.Success ? CliExitCodes.LegacyRuntimeSuccess : CliExitCodes.LegacyRuntimeFailure;
        }));

        return command;
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

    private static bool TryParseLastRunTime(string? rawLastRunTime, out DateTimeOffset? lastRunTime, out string? error)
    {
        lastRunTime = null;
        error = null;
        if (rawLastRunTime == null)
        {
            return true;
        }

        if (string.Equals(rawLastRunTime, "now", StringComparison.OrdinalIgnoreCase))
        {
            lastRunTime = DateTimeOffset.Now;
            return true;
        }

        if (DateTimeOffset.TryParse(rawLastRunTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            lastRunTime = parsed;
            return true;
        }

        error = $"Could not parse --last-run-time value '{rawLastRunTime}'. Use an ISO timestamp or 'now'.";
        return false;
    }

    private static bool ConfirmPatch(string markerId)
    {
        Console.Error.WriteLine($"WARNING: This will patch sync marker '{markerId}'. Type Y to continue, or anything else to cancel.");
        Console.Error.Write("> ");
        return string.Equals(Console.ReadLine(), "Y", StringComparison.OrdinalIgnoreCase);
    }

    private static void WritePatchSummary(PatchSyncMarkerResponse response)
    {
        if (CliOutputContext.Format != CliOutputFormat.Table)
        {
            return;
        }

        Console.WriteLine($"{response.Status}: sync marker {response.MarkerId}, changed {response.Changed.ToString().ToLowerInvariant()}.");
    }

    private static List<string> MarkerColumns(string? properties = null, string? additionalProperties = null)
    {
        var defaultProperties = string.Join(",", DefaultMarkerColumns());
        var columns = (properties ?? defaultProperties)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(property => property.Trim())
            .ToList();

        if (!string.IsNullOrWhiteSpace(additionalProperties))
        {
            columns.AddRange(additionalProperties.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(property => property.Trim()));
        }

        return columns;
    }

    private static List<string> DefaultMarkerColumns()
    {
        return
        [
            nameof(SyncMarker.id),
            nameof(SyncMarker.DataSource),
            nameof(SyncMarker.AgentId),
            nameof(SyncMarker.EntityType),
            nameof(SyncMarker.Value),
            nameof(SyncMarker.LastRunTime)
        ];
    }

    private static List<string> PatchColumns()
    {
        return
        [
            nameof(PatchSyncMarkerResponse.MarkerId),
            nameof(PatchSyncMarkerResponse.Status),
            nameof(PatchSyncMarkerResponse.Reason),
            nameof(PatchSyncMarkerResponse.Changed),
            $"{nameof(PatchSyncMarkerResponse.Result)}.{nameof(SyncMarker.DataSource)}",
            $"{nameof(PatchSyncMarkerResponse.Result)}.{nameof(SyncMarker.AgentId)}",
            $"{nameof(PatchSyncMarkerResponse.Result)}.{nameof(SyncMarker.EntityType)}",
            $"{nameof(PatchSyncMarkerResponse.Result)}.{nameof(SyncMarker.Value)}",
            $"{nameof(PatchSyncMarkerResponse.Result)}.{nameof(SyncMarker.LastRunTime)}"
        ];
    }
}
