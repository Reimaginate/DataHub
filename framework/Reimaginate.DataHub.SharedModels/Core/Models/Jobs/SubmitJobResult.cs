using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Core.Models.Jobs;

public class SubmitJobResult
{
    public JobDTO Result { get; set; }
    public bool Success { get; set; }
    public string FailureReason { get; set; }
   
}