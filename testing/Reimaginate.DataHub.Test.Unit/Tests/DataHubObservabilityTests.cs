using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Reimaginate.DataHub.AspNetCore;
using Reimaginate.DataHub.AspNetCore.Observability;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DataHubObservabilityTests
{
    [Fact]
    public void DataHubObservabilityOptions_should_default_to_minimal_cost_aware_profile()
    {
        var options = new DataHubObservabilityOptions();

        options.Profile.Should().Be(DataHubObservabilityProfile.Minimal);
        options.TracesPerSecond.Should().Be(1.0);
        options.MinimumLogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Error);
        options.EnableRequestPayloadTracing.Should().BeFalse();
        options.EnableResponsePayloadTracing.Should().BeFalse();
        options.EnableUnsafeMediatorPayloadTags.Should().BeFalse();
        options.AllowCallerRequestedTracing.Should().BeTrue();
        options.AllowCallerRequestedPayloadTracing.Should().BeFalse();
    }

    [Fact]
    public void DataHubTelemetryPayloadSanitizer_should_redact_sensitive_properties_and_truncate()
    {
        var options = new DataHubObservabilityOptions
        {
            MaxTelemetryPayloadCharacters = 80
        };

        var sanitized = DataHubTelemetryPayloadSanitizer.Sanitize(
            """
            {
              "name": "sync",
              "clientSecret": "super-secret",
              "nested": { "password": "p@ssw0rd" },
              "large": "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz"
            }
            """,
            options);

        sanitized.Should().Contain("\"clientSecret\":\"[redacted]\"");
        sanitized.Should().Contain("\"password\":\"[redacted]\"");
        sanitized.Should().NotContain("super-secret");
        sanitized.Should().NotContain("p@ssw0rd");
        sanitized.Should().EndWith("...[truncated]");
    }

    [Fact]
    public void Minimal_profile_should_not_add_payload_or_mediator_tags()
    {
        using var capture = new ActivityCapture();
        using var activity = capture.StartActivity();

        var options = new DataHubObservabilityOptions
        {
            Profile = DataHubObservabilityProfile.Minimal,
            EnableRequestPayloadTracing = true,
            EnableResponsePayloadTracing = true,
            EnableUnsafeMediatorPayloadTags = true
        };
        var request = SerializedRequest("corr-minimal", nameof(Minimal_profile_should_not_add_payload_or_mediator_tags));

        DataHubEndpointObservability.ApplyRequest(request, options, "client");
        DataHubEndpointObservability.ApplyResponse(new { ok = true }, options);

        activity.GetTagItem("datahub.correlation_id").Should().Be("corr-minimal");
        activity.GetTagItem("datahub.request.payload").Should().BeNull();
        activity.GetTagItem("datahub.response.payload").Should().BeNull();
        activity.GetTagItem("include_request").Should().BeNull();
        activity.GetTagItem("include_response").Should().BeNull();
    }

    [Fact]
    public void Diagnostic_profile_should_add_sanitized_payload_tags_for_matching_correlation_id()
    {
        using var capture = new ActivityCapture();
        using var activity = capture.StartActivity();
        var options = new DataHubObservabilityOptions
        {
            Profile = DataHubObservabilityProfile.Diagnostic,
            DiagnosticExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5),
            DiagnosticCorrelationIds = ["corr-diagnostic"],
            EnableRequestPayloadTracing = true,
            EnableResponsePayloadTracing = true,
            MaxTelemetryPayloadCharacters = 110
        };
        var request = SerializedRequest(
            "corr-diagnostic",
            nameof(Diagnostic_profile_should_add_sanitized_payload_tags_for_matching_correlation_id),
            """
            {
              "clientSecret": "super-secret",
              "payload": "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz"
            }
            """);

        DataHubEndpointObservability.ApplyRequest(request, options, "client");
        DataHubEndpointObservability.ApplyResponse(new
        {
            access_token = "token-value",
            payload = "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz"
        }, options);

        activity.GetTagItem("datahub.request.payload").Should().BeOfType<string>().Which
            .Should().Contain("\"clientSecret\":\"[redacted]\"");
        activity.GetTagItem("datahub.request.payload")!.ToString().Should().NotContain("super-secret");
        activity.GetTagItem("datahub.response.payload").Should().BeOfType<string>().Which
            .Should().Contain("\"access_token\":\"[redacted]\"");
        activity.GetTagItem("datahub.response.payload")!.ToString().Should().NotContain("token-value");
        activity.GetTagItem("include_request").Should().BeNull();
        activity.GetTagItem("include_response").Should().BeNull();
    }

    [Fact]
    public void Diagnostic_profile_should_not_capture_payloads_when_filter_or_ttl_does_not_match()
    {
        using var capture = new ActivityCapture();

        using (var expiredActivity = capture.StartActivity("expired"))
        {
            var expiredOptions = new DataHubObservabilityOptions
            {
                Profile = DataHubObservabilityProfile.Diagnostic,
                DiagnosticExpiresOn = DateTimeOffset.UtcNow.AddMinutes(-1),
                EnableRequestPayloadTracing = true,
                EnableResponsePayloadTracing = true
            };

            DataHubEndpointObservability.ApplyRequest(SerializedRequest("corr-expired", "ExpiredRequest"), expiredOptions, "client");
            DataHubEndpointObservability.ApplyResponse(new { secret = "should-not-emit" }, expiredOptions);

            expiredActivity.GetTagItem("datahub.request.payload").Should().BeNull();
            expiredActivity.GetTagItem("datahub.response.payload").Should().BeNull();
        }

        using (var nonMatchingActivity = capture.StartActivity("non-matching"))
        {
            var filteredOptions = new DataHubObservabilityOptions
            {
                Profile = DataHubObservabilityProfile.Diagnostic,
                DiagnosticExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5),
                DiagnosticCorrelationIds = ["corr-other"],
                DiagnosticRequestTypes = ["OtherRequest"],
                EnableRequestPayloadTracing = true,
                EnableResponsePayloadTracing = true
            };

            DataHubEndpointObservability.ApplyRequest(SerializedRequest("corr-no-match", "NoMatchRequest"), filteredOptions, "client");
            DataHubEndpointObservability.ApplyResponse(new { secret = "should-not-emit" }, filteredOptions);

            nonMatchingActivity.GetTagItem("datahub.request.payload").Should().BeNull();
            nonMatchingActivity.GetTagItem("datahub.response.payload").Should().BeNull();
        }
    }

    [Fact]
    public void Diagnostic_profile_should_add_unsafe_mediator_tags_only_when_enabled()
    {
        using var capture = new ActivityCapture();
        using var activity = capture.StartActivity();
        var options = new DataHubObservabilityOptions
        {
            Profile = DataHubObservabilityProfile.Diagnostic,
            EnableRequestPayloadTracing = true,
            EnableResponsePayloadTracing = true,
            EnableUnsafeMediatorPayloadTags = true
        };

        DataHubEndpointObservability.ApplyRequest(SerializedRequest("corr-unsafe", "UnsafeRequest"), options, "cli");

        activity.GetTagItem("include_request").Should().Be("true");
        activity.GetTagItem("include_response").Should().Be("true");
    }

    [Fact]
    public void Caller_trace_options_should_enable_diagnostic_trace_without_payload_by_default()
    {
        using var capture = new ActivityCapture();
        using var activity = capture.StartActivity();
        var options = new DataHubObservabilityOptions
        {
            Profile = DataHubObservabilityProfile.Minimal
        };
        var request = SerializedRequest("corr-caller", "CallerRequest", """{"clientSecret":"super-secret"}""");
        request.TraceOptions = new DataHubTraceOptions
        {
            Enabled = true,
            IncludeRequest = true,
            IncludeResponse = true,
            Reason = "investigate caller request"
        };

        DataHubEndpointObservability.ApplyRequest(request, options, "client");
        DataHubEndpointObservability.ApplyResponse(new { access_token = "token-value" }, options);

        activity.GetTagItem("datahub.diagnostic_trace").Should().Be("true");
        activity.GetTagItem("datahub.diagnostic_reason").Should().Be("investigate caller request");
        activity.GetTagItem("datahub.request.payload").Should().BeNull();
        activity.GetTagItem("datahub.response.payload").Should().BeNull();
        activity.GetTagItem("include_request").Should().BeNull();
        activity.GetTagItem("include_response").Should().BeNull();
    }

    [Fact]
    public void Caller_trace_options_should_capture_payloads_only_when_host_allows_payload_capture()
    {
        using var capture = new ActivityCapture();
        using var activity = capture.StartActivity();
        var options = new DataHubObservabilityOptions
        {
            Profile = DataHubObservabilityProfile.Minimal,
            AllowCallerRequestedPayloadTracing = true,
            MaxTelemetryPayloadCharacters = 120
        };
        var request = SerializedRequest("corr-caller-payload", "CallerPayloadRequest", """{"clientSecret":"super-secret","name":"sync"}""");
        request.TraceOptions = new DataHubTraceOptions
        {
            Enabled = true,
            IncludeRequest = true,
            IncludeResponse = true
        };

        DataHubEndpointObservability.ApplyRequest(request, options, "client");
        DataHubEndpointObservability.ApplyResponse(new { access_token = "token-value", ok = true }, options);

        activity.GetTagItem("datahub.request.payload").Should().BeOfType<string>().Which.Should().Contain("\"clientSecret\":\"[redacted]\"");
        activity.GetTagItem("datahub.request.payload")!.ToString().Should().NotContain("super-secret");
        activity.GetTagItem("datahub.response.payload").Should().BeOfType<string>().Which.Should().Contain("\"access_token\":\"[redacted]\"");
        activity.GetTagItem("datahub.response.payload")!.ToString().Should().NotContain("token-value");
    }

    [Fact]
    public void Caller_trace_options_should_ignore_expired_or_host_disabled_requests()
    {
        using var capture = new ActivityCapture();

        using (var expiredActivity = capture.StartActivity("expired-caller"))
        {
            var request = SerializedRequest("corr-expired-caller", "ExpiredCallerRequest");
            request.TraceOptions = new DataHubTraceOptions
            {
                Enabled = true,
                IncludeRequest = true,
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(-1)
            };

            DataHubEndpointObservability.ApplyRequest(request, new DataHubObservabilityOptions
            {
                Profile = DataHubObservabilityProfile.Minimal,
                AllowCallerRequestedPayloadTracing = true
            }, "client");

            expiredActivity.GetTagItem("datahub.diagnostic_trace").Should().BeNull();
            expiredActivity.GetTagItem("datahub.request.payload").Should().BeNull();
        }

        using (var disabledActivity = capture.StartActivity("host-disabled"))
        {
            var request = SerializedRequest("corr-host-disabled", "HostDisabledRequest");
            request.TraceOptions = new DataHubTraceOptions
            {
                Enabled = true,
                IncludeRequest = true
            };

            DataHubEndpointObservability.ApplyRequest(request, new DataHubObservabilityOptions
            {
                Profile = DataHubObservabilityProfile.Off,
                AllowCallerRequestedPayloadTracing = true
            }, "client");

            disabledActivity.GetTagItem("datahub.diagnostic_trace").Should().BeNull();
            disabledActivity.GetTagItem("datahub.request.payload").Should().BeNull();
        }
    }

    [Fact]
    public void Caller_trace_options_should_add_unsafe_mediator_tags_only_when_host_allows_them()
    {
        using var capture = new ActivityCapture();
        using var activity = capture.StartActivity();
        var request = SerializedRequest("corr-caller-unsafe", "CallerUnsafeRequest");
        request.TraceOptions = new DataHubTraceOptions
        {
            Enabled = true,
            IncludeRequest = true,
            IncludeResponse = true
        };

        DataHubEndpointObservability.ApplyRequest(request, new DataHubObservabilityOptions
        {
            Profile = DataHubObservabilityProfile.Minimal,
            AllowCallerRequestedPayloadTracing = true,
            EnableUnsafeMediatorPayloadTags = true
        }, "client");

        activity.GetTagItem("include_request").Should().Be("true");
        activity.GetTagItem("include_response").Should().Be("true");
    }

    [Fact]
    public void DataHubTelemetry_should_emit_expected_counter_names_and_tags()
    {
        using var capture = new MetricCapture();

        DataHubTelemetry.RecordRequestFailure(new DataHubErrorResponse
        {
            RequestType = "PatchEntityRequest",
            Category = DataHubErrorCategory.ServerError
        }, StatusCodes.Status500InternalServerError);
        DataHubTelemetry.RecordDomainFailure("sync", "log_sync_events", 2);
        DataHubTelemetry.RecordJobStatus("Ready", "DuplicateMerge", "DataMaintenanceAgent");
        DataHubTelemetry.RecordNotificationDispatchFailure("AlertNotification", 3);
        DataHubTelemetry.RecordProcessingLockTimeout("PatchEntityRequest");
        DataHubTelemetry.RecordPersistenceFailure("job_upsert", "Job");

        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.request.failure.count" &&
            metric.Value == 1 &&
            Equals(metric.Tags["request.type"], "PatchEntityRequest") &&
            Equals(metric.Tags["error.category"], DataHubErrorCategory.ServerError) &&
            Equals(metric.Tags["http.status_code"], StatusCodes.Status500InternalServerError));
        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.domain_failure.count" &&
            metric.Value == 2 &&
            Equals(metric.Tags["failure.type"], "sync") &&
            Equals(metric.Tags["source"], "log_sync_events"));
        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.job.status.count" &&
            Equals(metric.Tags["job.status"], "Ready") &&
            Equals(metric.Tags["job.type"], "DuplicateMerge") &&
            Equals(metric.Tags["job.target"], "DataMaintenanceAgent"));
        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.notification.dispatch.failure.count" &&
            metric.Value == 3 &&
            Equals(metric.Tags["notification.type"], "AlertNotification"));
        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.processing_lock.timeout.count" &&
            Equals(metric.Tags["operation"], "PatchEntityRequest"));
        capture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.persistence.failure.count" &&
            Equals(metric.Tags["operation"], "job_upsert") &&
            Equals(metric.Tags["document.type"], "Job"));
    }

    [Fact]
    public async Task CreateErrorResult_should_tag_current_activity_and_emit_request_failure_metric()
    {
        using var metricCapture = new MetricCapture("datahub.request.failure.count");
        using var activityCapture = new ActivityCapture();
        using var activity = activityCapture.StartActivity("parent");
        using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            Response =
            {
                Body = new MemoryStream()
            }
        };

        var result = DataHubEndpointDiagnostics.CreateErrorResult(
            new DataHubException(DataHubErrorCategory.ServerError, "server failed", "PatchEntityRequest", "corr-1"),
            context,
            NullLoggerFactory.Instance.CreateLogger("test"));

        await result.ExecuteAsync(context);

        activity!.GetTagItem("datahub.correlation_id").Should().Be("corr-1");
        activity.GetTagItem("datahub.request_type").Should().Be("PatchEntityRequest");
        activity.GetTagItem("datahub.error_category").Should().Be(DataHubErrorCategory.ServerError);
        activity.GetTagItem("datahub.http_status_code").Should().Be(StatusCodes.Status500InternalServerError);
        metricCapture.Measurements.Should().Contain(metric =>
            metric.Name == "datahub.request.failure.count" &&
            metric.Value == 1 &&
            Equals(metric.Tags["request.type"], "PatchEntityRequest"));

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = JsonConvert.DeserializeObject<DataHubErrorResponse>(body);
        error!.ErrorId.Should().NotBeNullOrWhiteSpace();
    }

    private static SerializedRequest SerializedRequest(string correlationId, string requestType, string data = """{"clientSecret":"super-secret"}""")
    {
        return new SerializedRequest
        {
            CorrelationId = correlationId,
            RequestType = requestType,
            Data = data
        };
    }
}
