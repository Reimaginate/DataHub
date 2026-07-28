using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateJobsResponse {
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<UpdateJobResponse> Results { get; set; }
}