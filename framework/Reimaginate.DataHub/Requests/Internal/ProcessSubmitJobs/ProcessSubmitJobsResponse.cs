using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;

namespace Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;

public class ProcessSubmitJobsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<SubmitJobResult> Results { get; set; }
}