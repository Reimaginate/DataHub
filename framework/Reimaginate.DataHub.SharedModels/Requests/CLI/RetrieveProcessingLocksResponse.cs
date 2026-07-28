using System.Collections.Generic;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RetrieveProcessingLocksResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<ProcessingLock> ProcessingLocks { get; set; }
}