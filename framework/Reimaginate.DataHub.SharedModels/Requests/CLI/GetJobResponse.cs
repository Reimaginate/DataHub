using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetJobResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public JobDTO Result { get; set; }
}