using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Diagnostics;

public class DataHubTraceQueryService : IDataHubTraceQueryService
{
    private readonly IAzureMonitorLogsQueryClient _client;
    private readonly DataHubTraceQueryOptions _options;

    public DataHubTraceQueryService(IAzureMonitorLogsQueryClient client, IOptions<DataHubTraceQueryOptions> options)
    {
        _client = client;
        _options = options.Value ?? new DataHubTraceQueryOptions();
    }

    public async Task<GetTraceResponse> QueryTraceAsync(GetTraceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TraceCorrelationId))
        {
            return Failure(request, "Correlation ID is required.");
        }

        var target = ResolveTarget();
        if (target == null)
        {
            return Failure(request, "DataHub observability logs query target is not configured. Set DataHub:Observability:LogsWorkspaceId or DataHub:Observability:LogsResourceId on the DataHub host.");
        }

        var window = ResolveWindow(request);
        var limit = ResolveLimit(request);
        var query = BuildTraceQuery(request.TraceCorrelationId, window.FromUtc, window.ToUtc, limit, request.IncludeLogs);
        var range = window.ToUtc - window.FromUtc;
        var rows = target.IsResource
            ? await _client.QueryResourceAsync(target.Value, query, range, cancellationToken)
            : await _client.QueryWorkspaceAsync(target.Value, query, range, cancellationToken);

        return new GetTraceResponse
        {
            Success = true,
            CorrelationId = request.TraceCorrelationId,
            FromUtc = window.FromUtc,
            ToUtc = window.ToUtc,
            Limit = limit,
            Records = rows.Select(row => ToTraceRecord(row, request.IncludePayloadDetails)).ToList()
        };
    }

    public string BuildTraceQuery(string correlationId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int limit, bool includeLogs)
    {
        var escapedCorrelationId = EscapeKqlString(correlationId);
        var from = fromUtc.UtcDateTime.ToString("O");
        var to = toUtc.UtcDateTime.ToString("O");
        var kinds = includeLogs
            ? @"""request"", ""dependency"", ""trace"", ""exception"""
            : @"""request"", ""dependency"", ""exception""";

        return $$"""
            let correlationId = "{{escapedCorrelationId}}";
            let fromUtc = datetime({{from}});
            let toUtc = datetime({{to}});
            let raw =
                union isfuzzy=true AppRequests, AppDependencies, AppTraces, AppExceptions
                | extend EventTime = coalesce(todatetime(column_ifexists("TimeGenerated", datetime(null))), todatetime(column_ifexists("timestamp", datetime(null))))
                | where EventTime between (fromUtc .. toUtc)
                | extend ItemType = tostring(column_ifexists("ItemType", column_ifexists("itemType", "")))
                | extend ItemType = iff(isempty(ItemType), case(
                    isnotempty(tostring(column_ifexists("DependencyType", ""))) or isnotempty(tostring(column_ifexists("Target", ""))), "dependency",
                    isnotempty(tostring(column_ifexists("ProblemId", ""))) or isnotempty(tostring(column_ifexists("ExceptionType", ""))), "exception",
                    isnotempty(tostring(column_ifexists("Message", ""))) and isempty(tostring(column_ifexists("DurationMs", ""))), "trace",
                    "request"), ItemType)
                | where ItemType in ({{kinds}})
                | extend Dimensions = todynamic(column_ifexists("Properties", column_ifexists("customDimensions", dynamic({}))))
                | extend OperationId = tostring(column_ifexists("OperationId", column_ifexists("operation_Id", "")))
                | extend SpanId = tostring(column_ifexists("Id", column_ifexists("id", "")))
                | extend ParentSpanId = tostring(column_ifexists("ParentId", column_ifexists("operation_ParentId", "")))
                | extend Name = tostring(column_ifexists("Name", column_ifexists("name", "")))
                | extend Message = tostring(column_ifexists("Message", column_ifexists("message", "")))
                | extend DurationMs = todouble(column_ifexists("DurationMs", column_ifexists("duration", real(null))))
                | extend Success = tobool(column_ifexists("Success", column_ifexists("success", bool(null))))
                | extend Severity = tostring(column_ifexists("SeverityLevel", column_ifexists("severityLevel", "")))
                | extend CloudRoleName = tostring(column_ifexists("CloudRoleName", column_ifexists("cloud_RoleName", "")))
                | extend RequestType = tostring(Dimensions["datahub.request_type"])
                | extend ErrorId = tostring(Dimensions["datahub.error_id"])
                | extend ErrorCategory = tostring(Dimensions["datahub.error_category"])
                | extend HttpStatusCode = toint(coalesce(tostring(Dimensions["datahub.http_status_code"]), tostring(Dimensions["http.response.status_code"]), tostring(column_ifexists("ResultCode", column_ifexists("resultCode", "")))))
                | extend DataHubCorrelationId = tostring(Dimensions["datahub.correlation_id"])
                | extend LegacyCorrelationId = tostring(Dimensions["correlation_id"])
                | extend HasCorrelation =
                    DataHubCorrelationId =~ correlationId
                    or LegacyCorrelationId =~ correlationId
                    or OperationId =~ correlationId
                    or SpanId =~ correlationId
                    or ParentSpanId =~ correlationId
                | project EventTime, ItemType, OperationId, SpanId, ParentSpanId, Name, Message, DurationMs, Success, Severity, CloudRoleName, RequestType, ErrorId, ErrorCategory, HttpStatusCode, Dimensions, HasCorrelation;
            let operationIds = raw
                | where HasCorrelation
                | where isnotempty(OperationId)
                | distinct OperationId;
            raw
            | where HasCorrelation or OperationId in (operationIds)
            | order by EventTime asc
            | take {{limit}}
            | project
                StartTimeUtc = EventTime,
                ItemType,
                OperationId,
                SpanId,
                ParentSpanId,
                Name,
                Message,
                DurationMs,
                Success,
                Severity,
                CloudRoleName,
                RequestType,
                ErrorId,
                ErrorCategory,
                HttpStatusCode,
                TagsJson = tostring(bag_pack(
                    "datahub.endpoint", tostring(Dimensions["datahub.endpoint"]),
                    "datahub.correlation_id", tostring(Dimensions["datahub.correlation_id"]),
                    "correlation_id", tostring(Dimensions["correlation_id"]))),
                DetailsJson = tostring(Dimensions)
            """;
    }

    private TraceRecord ToTraceRecord(AzureMonitorTraceRow row, bool includePayloadDetails)
    {
        return new TraceRecord
        {
            OperationId = row.OperationId,
            SpanId = string.IsNullOrWhiteSpace(row.SpanId) ? row.OperationId : row.SpanId,
            ParentSpanId = row.ParentSpanId,
            Kind = row.ItemType,
            Name = FirstNonEmpty(row.Name, row.Message, row.ItemType),
            Message = row.Message,
            StartTimeUtc = row.StartTimeUtc ?? DateTimeOffset.MinValue,
            DurationMs = row.DurationMs,
            Success = row.Success,
            Severity = row.Severity,
            CloudRoleName = row.CloudRoleName,
            RequestType = row.RequestType,
            ErrorId = row.ErrorId,
            ErrorCategory = row.ErrorCategory,
            HttpStatusCode = row.HttpStatusCode,
            Tags = ParseSafeTags(row.TagsJson),
            Details = ParseDetails(row.DetailsJson, includePayloadDetails)
        };
    }

    private (DateTimeOffset FromUtc, DateTimeOffset ToUtc) ResolveWindow(GetTraceRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var maxLookback = Math.Max(1, _options.MaxLookbackHours);
        var defaultLookback = Math.Clamp(_options.DefaultLookbackHours <= 0 ? DataHubTraceQueryOptions.DefaultDefaultLookbackHours : _options.DefaultLookbackHours, 1, maxLookback);
        var requestedLookback = Math.Clamp(request.LookbackHours ?? defaultLookback, 1, maxLookback);
        var to = (request.ToUtc ?? now).ToUniversalTime();
        var from = request.FromUtc?.ToUniversalTime() ?? to.AddHours(-requestedLookback);
        var earliest = to.AddHours(-maxLookback);
        if (from < earliest)
        {
            from = earliest;
        }

        if (from > to)
        {
            from = to.AddHours(-requestedLookback);
        }

        return (from, to);
    }

    private int ResolveLimit(GetTraceRequest request)
    {
        var max = Math.Max(1, _options.MaxResultCount);
        var configuredDefault = _options.DefaultResultCount <= 0 ? DataHubTraceQueryOptions.DefaultDefaultResultCount : _options.DefaultResultCount;
        return Math.Clamp(request.Limit ?? configuredDefault, 1, max);
    }

    private LogsQueryTarget ResolveTarget()
    {
        if (!string.IsNullOrWhiteSpace(_options.LogsResourceId))
        {
            return new LogsQueryTarget(_options.LogsResourceId, true);
        }

        if (!string.IsNullOrWhiteSpace(_options.LogsWorkspaceId))
        {
            return new LogsQueryTarget(_options.LogsWorkspaceId, false);
        }

        return null;
    }

    private static GetTraceResponse Failure(GetTraceRequest request, string reason)
    {
        var to = DateTimeOffset.UtcNow;
        return new GetTraceResponse
        {
            Success = false,
            FailureReason = reason,
            CorrelationId = request?.TraceCorrelationId,
            FromUtc = to.AddHours(-DataHubTraceQueryOptions.DefaultDefaultLookbackHours),
            ToUtc = to,
            Limit = DataHubTraceQueryOptions.DefaultDefaultResultCount
        };
    }

    private static Dictionary<string, string> ParseSafeTags(string tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var parsed = JObject.Parse(tagsJson);
            return parsed.Properties()
                .Where(property => !string.IsNullOrWhiteSpace(property.Value?.ToString()))
                .ToDictionary(property => property.Name, property => property.Value.ToString(Formatting.None).Trim('"'), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static Dictionary<string, string> ParseDetails(string detailsJson, bool includePayloadDetails)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var parsed = JObject.Parse(detailsJson);
            return parsed.Properties()
                .Where(property => !string.IsNullOrWhiteSpace(property.Value?.ToString()))
                .Where(property => includePayloadDetails || !IsPayloadDetail(property.Name))
                .ToDictionary(
                    property => property.Name,
                    property => property.Value.ToString(Formatting.None).Trim('"'),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static bool IsPayloadDetail(string key)
        => !string.IsNullOrWhiteSpace(key) && key.Contains("payload", StringComparison.OrdinalIgnoreCase);

    private static string EscapeKqlString(string value)
        => value?.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) ?? string.Empty;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record LogsQueryTarget(string Value, bool IsResource);
}
