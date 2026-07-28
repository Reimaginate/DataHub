using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.GetLogs;

public class GetLogsResponse
{
    public List<LogEntry> Results { get; set; }

    public int ResultCount { get; set; }

    public string ContinuationToken { get; set; }

    public bool MoreResultsAvailable { get; set; }
}