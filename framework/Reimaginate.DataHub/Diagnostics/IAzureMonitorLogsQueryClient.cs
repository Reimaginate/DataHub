using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Reimaginate.DataHub.Diagnostics;

public interface IAzureMonitorLogsQueryClient
{
    Task<IReadOnlyList<AzureMonitorTraceRow>> QueryWorkspaceAsync(string workspaceId, string query, TimeSpan timeRange, CancellationToken cancellationToken);
    Task<IReadOnlyList<AzureMonitorTraceRow>> QueryResourceAsync(string resourceId, string query, TimeSpan timeRange, CancellationToken cancellationToken);
}
