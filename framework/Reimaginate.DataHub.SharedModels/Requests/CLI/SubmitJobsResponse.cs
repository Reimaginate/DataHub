using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class SubmitJobsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<Core.Models.Jobs.SubmitJobResult> Results { get; set; }
}