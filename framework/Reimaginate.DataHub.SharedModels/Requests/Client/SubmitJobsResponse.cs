using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class SubmitJobsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<SubmitJobResult> Results { get; set; }
}