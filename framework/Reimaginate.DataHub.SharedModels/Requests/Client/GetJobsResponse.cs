using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetJobsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<JobDTO> Results { get; set; }
    public int ResultCount { get; set; }
    public string ContinuationToken { get; set; }
    public bool MoreResultsAvailable { get; set; }
}