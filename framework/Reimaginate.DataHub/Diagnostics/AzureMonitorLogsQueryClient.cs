using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query.Logs;
using Azure.Monitor.Query.Logs.Models;

namespace Reimaginate.DataHub.Diagnostics;

public class AzureMonitorLogsQueryClient : IAzureMonitorLogsQueryClient
{
    private readonly LogsQueryClient _client;

    public AzureMonitorLogsQueryClient()
        : this(new DefaultAzureCredential())
    {
    }

    public AzureMonitorLogsQueryClient(TokenCredential credential)
    {
        _client = new LogsQueryClient(credential);
    }

    public async Task<IReadOnlyList<AzureMonitorTraceRow>> QueryWorkspaceAsync(
        string workspaceId,
        string query,
        TimeSpan timeRange,
        CancellationToken cancellationToken)
    {
        var response = await _client.QueryWorkspaceAsync<AzureMonitorTraceRow>(
            workspaceId,
            query,
            new LogsQueryTimeRange(timeRange),
            cancellationToken: cancellationToken);
        return response.Value;
    }

    public async Task<IReadOnlyList<AzureMonitorTraceRow>> QueryResourceAsync(
        string resourceId,
        string query,
        TimeSpan timeRange,
        CancellationToken cancellationToken)
    {
        var response = await _client.QueryResourceAsync<AzureMonitorTraceRow>(
            new ResourceIdentifier(resourceId),
            query,
            new LogsQueryTimeRange(timeRange),
            cancellationToken: cancellationToken);
        return response.Value;
    }
}
