using System.Collections.Generic;

namespace Reimaginate.DataHub.Requests.Internal.DispatchJobs;

public class DispatchJobsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<DispatchJobResult> Results { get; set; } 
}