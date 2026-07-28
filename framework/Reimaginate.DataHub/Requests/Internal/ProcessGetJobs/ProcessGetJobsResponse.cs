using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataServices.Responses;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetJobs;

public class ProcessGetJobsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public PagedResults<Job> PagedResults { get; set; }

}