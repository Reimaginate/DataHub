using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.Requests.Internal.DispatchJobs;

public class DispatchJobResult
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public JobDTO Result { get; set; }
}