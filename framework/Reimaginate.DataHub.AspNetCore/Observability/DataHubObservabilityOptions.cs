using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Reimaginate.DataHub.AspNetCore.Observability;

public sealed class DataHubObservabilityOptions
{
    public DataHubObservabilityProfile Profile { get; set; } = DataHubObservabilityProfile.Minimal;
    public string CloudRoleName { get; set; } = "DataHub";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string EnvironmentName { get; set; }
    public string DeploymentId { get; set; }
    public string ConnectionString { get; set; }
    public double? TracesPerSecond { get; set; } = 1.0;
    public float? SamplingRatio { get; set; }
    public bool EnableLiveMetrics { get; set; }
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Error;
    public bool EnableRequestPayloadTracing { get; set; }
    public bool EnableResponsePayloadTracing { get; set; }
    public bool EnableUnsafeMediatorPayloadTags { get; set; }
    public bool AllowCallerRequestedTracing { get; set; } = true;
    public bool AllowCallerRequestedPayloadTracing { get; set; }
    public int? MaxCallerTraceDurationMinutes { get; set; }
    public DateTimeOffset? DiagnosticExpiresOn { get; set; }
    public List<string> DiagnosticCorrelationIds { get; set; } = new();
    public List<string> DiagnosticRequestTypes { get; set; } = new();
    public int MaxTelemetryPayloadCharacters { get; set; } = 4096;
    public List<string> RedactedPropertyNames { get; set; } =
    [
        "authorization",
        "access_token",
        "refresh_token",
        "token",
        "password",
        "secret",
        "clientsecret",
        "client_secret",
        "connectionstring",
        "connstring",
        "key",
        "accountkey",
        "eventgridkey"
    ];

    public bool IsEnabled => Profile != DataHubObservabilityProfile.Off;

    public bool IsDiagnosticEnabled(DateTimeOffset now)
    {
        if (Profile != DataHubObservabilityProfile.Diagnostic)
        {
            return false;
        }

        return DiagnosticExpiresOn == null || DiagnosticExpiresOn > now;
    }
}
