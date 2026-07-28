using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class UpdateJobsResponse {
    public bool Success { get; set; }
    public string FailureReason { get; set; }

    public List<UpdateJobResult> Results { get; set; }
}