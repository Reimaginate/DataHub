using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetLogEntriesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public string Select { get; set; }
    public List<LogEntry> Results { get; set; }
    public int ResultCount { get; set; }
    public string ContinuationToken { get; set; }
    public bool MoreResultsAvailable { get; set; }
}
