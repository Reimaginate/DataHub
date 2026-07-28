using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;

namespace Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;

public class DeleteLogEntriesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<DeleteLogFailure> DeleteFailures { get; set; }
}
