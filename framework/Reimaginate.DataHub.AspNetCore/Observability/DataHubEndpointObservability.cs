using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.AspNetCore.Observability;

internal static class DataHubEndpointObservability
{
    private const string DiagnosticResponsePayloadEnabledProperty = "datahub.diagnostic_response_payload_enabled";

    public static void ApplyRequest(
        SerializedRequest serializedRequest,
        DataHubObservabilityOptions options,
        string endpoint)
    {
        var activity = Activity.Current;
        if (activity == null || serializedRequest == null)
        {
            return;
        }

        activity.SetTag("correlation_id", serializedRequest.CorrelationId);
        activity.SetTag("datahub.correlation_id", serializedRequest.CorrelationId);
        activity.SetTag("datahub.request_type", serializedRequest.RequestType);
        activity.SetTag("datahub.endpoint", endpoint);

        var diagnosticDecision = ResolveDiagnosticDecision(options, serializedRequest, DateTimeOffset.UtcNow);
        if (!diagnosticDecision.Enabled)
        {
            return;
        }

        activity.SetTag("datahub.diagnostic_trace", "true");
        if (!string.IsNullOrWhiteSpace(serializedRequest.TraceOptions?.Reason))
        {
            activity.SetTag("datahub.diagnostic_reason", serializedRequest.TraceOptions.Reason);
        }

        if (diagnosticDecision.IncludeResponse)
        {
            activity.SetCustomProperty(DiagnosticResponsePayloadEnabledProperty, true);
        }

        if (options.EnableUnsafeMediatorPayloadTags)
        {
            if (diagnosticDecision.IncludeRequest)
            {
                activity.SetTag("include_request", "true");
            }

            if (diagnosticDecision.IncludeResponse)
            {
                activity.SetTag("include_response", "true");
            }
        }

        if (diagnosticDecision.IncludeRequest)
        {
            activity.SetTag(
                "datahub.request.payload",
                DataHubTelemetryPayloadSanitizer.Sanitize(serializedRequest.Data, options));
        }
    }

    public static void ApplyResponse(object response, DataHubObservabilityOptions options)
    {
        var activity = Activity.Current;
        if (activity == null ||
            activity.GetCustomProperty(DiagnosticResponsePayloadEnabledProperty) is not true)
        {
            return;
        }

        try
        {
            var serializedResponse = JsonConvert.SerializeObject(response);
            activity.SetTag(
                "datahub.response.payload",
                DataHubTelemetryPayloadSanitizer.Sanitize(serializedResponse, options));
        }
        catch (JsonException)
        {
            activity.SetTag("datahub.response.payload", string.Empty);
        }
    }

    public static void ApplyError(DataHubErrorResponse errorResponse, int statusCode)
    {
        var activity = Activity.Current;
        if (activity == null || errorResponse == null)
        {
            return;
        }

        activity.SetTag("correlation_id", errorResponse.CorrelationId);
        activity.SetTag("datahub.correlation_id", errorResponse.CorrelationId);
        activity.SetTag("datahub.request_type", errorResponse.RequestType);
        activity.SetTag("datahub.error_id", errorResponse.ErrorId);
        activity.SetTag("datahub.error_category", errorResponse.Category);
        activity.SetTag("datahub.http_status_code", statusCode);
        activity.SetTag("http.response.status_code", statusCode);
        activity.SetStatus(statusCode >= 500 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
    }

    private static DiagnosticDecision ResolveDiagnosticDecision(
        DataHubObservabilityOptions options,
        SerializedRequest serializedRequest,
        DateTimeOffset now)
    {
        if (options == null || !options.IsEnabled)
        {
            return DiagnosticDecision.Disabled;
        }

        var callerDecision = ResolveCallerDecision(options, serializedRequest.TraceOptions, now);
        var hostDecision = ResolveHostDiagnosticDecision(options, serializedRequest, now);

        return new DiagnosticDecision(
            callerDecision.Enabled || hostDecision.Enabled,
            callerDecision.IncludeRequest || hostDecision.IncludeRequest,
            callerDecision.IncludeResponse || hostDecision.IncludeResponse);
    }

    private static DiagnosticDecision ResolveCallerDecision(
        DataHubObservabilityOptions options,
        DataHubTraceOptions traceOptions,
        DateTimeOffset now)
    {
        if (!options.AllowCallerRequestedTracing || traceOptions?.Enabled != true)
        {
            return DiagnosticDecision.Disabled;
        }

        if (traceOptions.ExpiresOn.HasValue && traceOptions.ExpiresOn <= now)
        {
            return DiagnosticDecision.Disabled;
        }

        if (options.MaxCallerTraceDurationMinutes.HasValue &&
            traceOptions.ExpiresOn.HasValue &&
            traceOptions.ExpiresOn.Value > now.AddMinutes(options.MaxCallerTraceDurationMinutes.Value))
        {
            return DiagnosticDecision.Disabled;
        }

        var allowPayloads = options.AllowCallerRequestedPayloadTracing;
        return new DiagnosticDecision(
            true,
            allowPayloads && traceOptions.IncludeRequest,
            allowPayloads && traceOptions.IncludeResponse);
    }

    private static DiagnosticDecision ResolveHostDiagnosticDecision(
        DataHubObservabilityOptions options,
        SerializedRequest serializedRequest,
        DateTimeOffset now)
    {
        if (!IsDiagnosticMatch(options, serializedRequest, now))
        {
            return DiagnosticDecision.Disabled;
        }

        return new DiagnosticDecision(
            true,
            options.EnableRequestPayloadTracing,
            options.EnableResponsePayloadTracing);
    }

    private static bool IsDiagnosticMatch(DataHubObservabilityOptions options, SerializedRequest serializedRequest, DateTimeOffset now)
    {
        if (options == null || !options.IsDiagnosticEnabled(now))
        {
            return false;
        }

        var hasCorrelationFilter = options.DiagnosticCorrelationIds?.Any() == true;
        var hasRequestTypeFilter = options.DiagnosticRequestTypes?.Any() == true;

        if (!hasCorrelationFilter && !hasRequestTypeFilter)
        {
            return true;
        }

        return (hasCorrelationFilter && options.DiagnosticCorrelationIds.Contains(serializedRequest.CorrelationId, StringComparer.OrdinalIgnoreCase)) ||
               (hasRequestTypeFilter && options.DiagnosticRequestTypes.Contains(serializedRequest.RequestType, StringComparer.OrdinalIgnoreCase));
    }

    private readonly record struct DiagnosticDecision(bool Enabled, bool IncludeRequest, bool IncludeResponse)
    {
        public static DiagnosticDecision Disabled { get; } = new(false, false, false);
    }
}
