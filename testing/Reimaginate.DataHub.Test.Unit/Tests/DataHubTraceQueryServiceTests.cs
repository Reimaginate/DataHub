using FluentAssertions;
using Microsoft.Extensions.Options;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DataHubTraceQueryServiceTests
{
    [Fact]
    public async Task QueryTraceAsync_returns_clear_failure_when_query_target_is_missing()
    {
        var client = new CapturingLogsQueryClient();
        var service = CreateService(client, new DataHubTraceQueryOptions());

        var response = await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1" }, CancellationToken.None);

        response.Success.Should().BeFalse();
        response.FailureReason.Should().Contain("DataHub observability logs query target is not configured");
        client.QueryCount.Should().Be(0);
    }

    [Fact]
    public async Task QueryTraceAsync_caps_lookback_and_limit_from_server_options()
    {
        var client = new CapturingLogsQueryClient();
        var service = CreateService(client, new DataHubTraceQueryOptions
        {
            LogsWorkspaceId = "workspace-1",
            MaxLookbackHours = 2,
            MaxResultCount = 25
        });

        var response = await service.QueryTraceAsync(new GetTraceRequest
        {
            TraceCorrelationId = "corr-1",
            LookbackHours = 48,
            Limit = 500
        }, CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Limit.Should().Be(25);
        (response.ToUtc - response.FromUtc).TotalHours.Should().BeLessThanOrEqualTo(2.01);
        client.Query.Should().Contain("take 25");
    }

    [Fact]
    public async Task QueryTraceAsync_uses_workspace_target_and_safe_app_insights_tables()
    {
        var client = new CapturingLogsQueryClient
        {
            Rows =
            [
                new AzureMonitorTraceRow
                {
                    StartTimeUtc = DateTimeOffset.Parse("2026-06-20T00:00:00Z"),
                    ItemType = "request",
                    OperationId = "op-1",
                    SpanId = "span-1",
                    Name = "GET /api/CLI",
                    TagsJson = """{"datahub.correlation_id":"corr-1"}"""
                }
            ]
        };
        var service = CreateService(client, new DataHubTraceQueryOptions { LogsWorkspaceId = "workspace-1" });

        var response = await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1" }, CancellationToken.None);

        client.WorkspaceId.Should().Be("workspace-1");
        client.ResourceId.Should().BeNull();
        client.Query.Should().Contain("AppRequests");
        client.Query.Should().Contain("AppDependencies");
        client.Query.Should().Contain("AppTraces");
        client.Query.Should().Contain("AppExceptions");
        client.Query.Should().NotContain("traces");
        client.Query.Should().NotContain("exceptions");
        client.Query.Should().Contain("datahub.correlation_id");
        client.Query.Should().Contain("correlation_id");
        client.Query.Should().Contain("operationIds");
        client.Query.Should().Contain("DetailsJson = tostring(Dimensions)");
        response.Records.Should().ContainSingle().Which.Tags.Should().ContainKey("datahub.correlation_id");
    }

    [Fact]
    public async Task QueryTraceAsync_maps_full_dimensions_to_trace_record_details()
    {
        var client = new CapturingLogsQueryClient
        {
            Rows =
            [
                new AzureMonitorTraceRow
                {
                    OperationId = "op-1",
                    SpanId = "span-1",
                    Message = "Dependency failed.",
                    DetailsJson = """{"custom.dimension":"value","datahub.error_id":"err-1"}"""
                }
            ]
        };
        var service = CreateService(client, new DataHubTraceQueryOptions { LogsWorkspaceId = "workspace-1" });

        var response = await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1" }, CancellationToken.None);

        var record = response.Records.Should().ContainSingle().Subject;
        record.Message.Should().Be("Dependency failed.");
        record.Details.Should().Contain("custom.dimension", "value");
        record.Details.Should().Contain("datahub.error_id", "err-1");
    }

    [Fact]
    public async Task QueryTraceAsync_excludes_payload_dimensions_from_trace_record_details_by_default()
    {
        var client = new CapturingLogsQueryClient
        {
            Rows =
            [
                new AzureMonitorTraceRow
                {
                    OperationId = "op-1",
                    SpanId = "span-1",
                    DetailsJson = """{"custom.dimension":"value","datahub.request.payload":"secret-request","datahub.response.payload":"secret-response"}"""
                }
            ]
        };
        var service = CreateService(client, new DataHubTraceQueryOptions { LogsWorkspaceId = "workspace-1" });

        var response = await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1" }, CancellationToken.None);

        var details = response.Records.Should().ContainSingle().Subject.Details;
        details.Should().Contain("custom.dimension", "value");
        details.Should().NotContainKey("datahub.request.payload");
        details.Should().NotContainKey("datahub.response.payload");
    }

    [Fact]
    public async Task QueryTraceAsync_includes_payload_dimensions_when_requested()
    {
        var client = new CapturingLogsQueryClient
        {
            Rows =
            [
                new AzureMonitorTraceRow
                {
                    OperationId = "op-1",
                    SpanId = "span-1",
                    DetailsJson = """{"custom.dimension":"value","datahub.request.payload":"secret-request","datahub.response.payload":"secret-response"}"""
                }
            ]
        };
        var service = CreateService(client, new DataHubTraceQueryOptions { LogsWorkspaceId = "workspace-1" });

        var response = await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1", IncludePayloadDetails = true }, CancellationToken.None);

        var details = response.Records.Should().ContainSingle().Subject.Details;
        details.Should().Contain("custom.dimension", "value");
        details.Should().Contain("datahub.request.payload", "secret-request");
        details.Should().Contain("datahub.response.payload", "secret-response");
    }

    [Fact]
    public async Task QueryTraceAsync_seeds_matches_from_correlation_fields_and_operation_ids_only()
    {
        var client = new CapturingLogsQueryClient();
        var service = CreateService(client, new DataHubTraceQueryOptions { LogsWorkspaceId = "workspace-1" });

        await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1" }, CancellationToken.None);

        client.Query.Should().NotContain("pack_all()) contains correlationId");
        client.Query.Should().Contain("""DataHubCorrelationId = tostring(Dimensions["datahub.correlation_id"])""");
        client.Query.Should().Contain("""LegacyCorrelationId = tostring(Dimensions["correlation_id"])""");
        client.Query.Should().Contain("DataHubCorrelationId =~ correlationId");
        client.Query.Should().Contain("LegacyCorrelationId =~ correlationId");
        client.Query.Should().Contain("OperationId =~ correlationId");
        client.Query.Should().Contain("SpanId =~ correlationId");
        client.Query.Should().Contain("ParentSpanId =~ correlationId");
    }

    [Fact]
    public async Task QueryTraceAsync_does_not_seed_match_from_diagnostics_request_payload_containing_requested_correlation_id()
    {
        var client = new CapturingLogsQueryClient();
        var service = CreateService(client, new DataHubTraceQueryOptions { LogsWorkspaceId = "workspace-1" });

        await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1" }, CancellationToken.None);

        client.Query.Should().NotContain("datahub.request.payload");
        client.Query.Should().NotContain("datahub.response.payload");
        client.Query.Should().NotContain("Name contains correlationId");
        client.Query.Should().NotContain("Message contains correlationId");
        client.Query.Should().NotContain("tostring(pack_all()) contains correlationId");
    }

    [Fact]
    public async Task QueryTraceAsync_uses_resource_target_when_configured()
    {
        var client = new CapturingLogsQueryClient();
        var service = CreateService(client, new DataHubTraceQueryOptions
        {
            LogsWorkspaceId = "workspace-1",
            LogsResourceId = "/subscriptions/sub-1/resourceGroups/rg/providers/microsoft.insights/components/appi"
        });

        await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1" }, CancellationToken.None);

        client.ResourceId.Should().Be("/subscriptions/sub-1/resourceGroups/rg/providers/microsoft.insights/components/appi");
        client.WorkspaceId.Should().BeNull();
    }

    [Fact]
    public async Task QueryTraceAsync_excludes_trace_logs_unless_requested()
    {
        var client = new CapturingLogsQueryClient();
        var service = CreateService(client, new DataHubTraceQueryOptions { LogsWorkspaceId = "workspace-1" });

        await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1", IncludeLogs = false }, CancellationToken.None);
        client.Query.Should().Contain(@"""request"", ""dependency"", ""exception""");

        await service.QueryTraceAsync(new GetTraceRequest { TraceCorrelationId = "corr-1", IncludeLogs = true }, CancellationToken.None);
        client.Query.Should().Contain(@"""request"", ""dependency"", ""trace"", ""exception""");
    }

    private static DataHubTraceQueryService CreateService(CapturingLogsQueryClient client, DataHubTraceQueryOptions options)
        => new(client, Options.Create(options));

    private sealed class CapturingLogsQueryClient : IAzureMonitorLogsQueryClient
    {
        public List<AzureMonitorTraceRow> Rows { get; set; } = [];
        public string? WorkspaceId { get; private set; }
        public string? ResourceId { get; private set; }
        public string? Query { get; private set; }
        public int QueryCount { get; private set; }

        public Task<IReadOnlyList<AzureMonitorTraceRow>> QueryWorkspaceAsync(string workspaceId, string query, TimeSpan timeRange, CancellationToken cancellationToken)
        {
            QueryCount++;
            WorkspaceId = workspaceId;
            ResourceId = null;
            Query = query;
            return Task.FromResult<IReadOnlyList<AzureMonitorTraceRow>>(Rows);
        }

        public Task<IReadOnlyList<AzureMonitorTraceRow>> QueryResourceAsync(string resourceId, string query, TimeSpan timeRange, CancellationToken cancellationToken)
        {
            QueryCount++;
            ResourceId = resourceId;
            WorkspaceId = null;
            Query = query;
            return Task.FromResult<IReadOnlyList<AzureMonitorTraceRow>>(Rows);
        }
    }
}
