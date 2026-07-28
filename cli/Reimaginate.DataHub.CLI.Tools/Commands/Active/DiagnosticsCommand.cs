using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Commands.Diagnostics;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

public sealed class DiagnosticsCommand : DataHubTopLevelCommand
{
    public DiagnosticsCommand(IServiceProvider serviceProvider) : base("diagnostics", serviceProvider)
    {
        Description = "Inspect production diagnostics.";
        Add(Trace(serviceProvider));
    }

    private static Command Trace(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("trace", "Render a DataHub trace by correlation ID.", out var output);
        var correlationId = ActiveCommandFactory.RequiredArgument("correlation-id", "DataHub correlation ID.");
        var lookbackHours = ActiveCommandFactory.IntOption("--lookback-hours", "Lookback window in hours.");
        var from = ActiveCommandFactory.StringOption("--from", "UTC trace query start time.");
        var to = ActiveCommandFactory.StringOption("--to", "UTC trace query end time.");
        var limit = ActiveCommandFactory.IntOption("--limit", "Maximum telemetry records to return.");
        var includeLogs = ActiveCommandFactory.BoolOption("--include-logs", "Include trace log rows as tree nodes.");
        var details = ActiveCommandFactory.BoolOption("--details", "Print detailed telemetry records after the trace tree.");
        var record = ActiveCommandFactory.IntOption("--record", "Print details for a rendered trace record number.");
        var spanId = ActiveCommandFactory.StringOption("--span-id", "Print details for a telemetry span id.");
        var operationId = ActiveCommandFactory.StringOption("--operation-id", "Print details for a telemetry operation id.");
        var includePayloads = ActiveCommandFactory.BoolOption("--include-payloads", "Include payload dimensions in trace details.");
        ActiveCommandFactory.Add(command, correlationId, lookbackHours, from, to, limit, includeLogs, details, record, spanId, operationId, includePayloads);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, async () =>
        {
            var detailRecordNumber = parse.GetValue(record);
            if (detailRecordNumber.HasValue && detailRecordNumber <= 0)
            {
                throw new ArgumentException("--record must be greater than zero.");
            }

            var request = new GetTraceRequest
            {
                TraceCorrelationId = parse.GetValue(correlationId)!,
                LookbackHours = parse.GetValue(lookbackHours),
                FromUtc = ParseDateTimeOffset(parse.GetValue(from), "--from"),
                ToUtc = ParseDateTimeOffset(parse.GetValue(to), "--to"),
                Limit = parse.GetValue(limit),
                IncludeLogs = parse.GetValue(includeLogs),
                IncludePayloadDetails = parse.GetValue(includePayloads)
            };

            var adminApi = services.GetRequiredService<ICLIApi>();
            var response = await adminApi.PostAdminMessage<GetTraceResponse>(new SerializedRequest
            {
                RequestType = nameof(GetTraceRequest),
                Data = JsonConvert.SerializeObject(request)
            }, ct);

            if (CliOutputContext.Format != CliOutputFormat.Table)
            {
                ConsoleHelper.PrintTable(response.Records ?? [], [
                    nameof(TraceRecord.OperationId),
                    nameof(TraceRecord.SpanId),
                    nameof(TraceRecord.ParentSpanId),
                    nameof(TraceRecord.Kind),
                    nameof(TraceRecord.Name),
                    nameof(TraceRecord.StartTimeUtc),
                    nameof(TraceRecord.DurationMs),
                    nameof(TraceRecord.Success),
                    nameof(TraceRecord.Severity),
                    nameof(TraceRecord.CloudRoleName),
                    nameof(TraceRecord.RequestType),
                    nameof(TraceRecord.ErrorId),
                    nameof(TraceRecord.ErrorCategory),
                    nameof(TraceRecord.HttpStatusCode)
                ]);
                return CliExitCodes.LegacyRuntimeSuccess;
            }

            if (!response.Success)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{response.FailureReason.EscapeMarkup()}[/]");
                return CliExitCodes.LegacyRuntimeFailure;
            }

            if (response.Records == null || response.Records.Count == 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]No trace found for correlation ID '{request.TraceCorrelationId.EscapeMarkup()}' between {response.FromUtc:u} and {response.ToUtc:u}.[/]");
                return CliExitCodes.LegacyRuntimeSuccess;
            }

            AnsiConsole.Write(TraceTreeRenderer.Build(response));
            AnsiConsole.WriteLine();
            if (parse.GetValue(details) || detailRecordNumber.HasValue || !string.IsNullOrWhiteSpace(parse.GetValue(spanId)) || !string.IsNullOrWhiteSpace(parse.GetValue(operationId)))
            {
                TraceTreeRenderer.WriteDetails(
                    response,
                    detailRecordNumber,
                    parse.GetValue(spanId),
                    parse.GetValue(operationId));
            }

            return CliExitCodes.LegacyRuntimeSuccess;
        }));
        return command;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value, string optionName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        throw new ArgumentException($"{optionName} must be a valid date/time value.");
    }
}
