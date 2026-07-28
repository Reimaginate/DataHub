using System.Collections.Generic;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.LogEvents;

public class LogEventsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<LogEntry> LogEntries { get; set; }
    public List<DataAccessFailure<LogEntry>> Failures { get; set; } = new();
}
