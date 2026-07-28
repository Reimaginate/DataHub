using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.Requests.Internal.RegisterJobs;

public class RegisterJobResult
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public JobDTO Job { get; set; }
}