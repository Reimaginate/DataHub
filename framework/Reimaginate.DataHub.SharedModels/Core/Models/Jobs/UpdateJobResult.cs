namespace Reimaginate.DataHub.SharedModels.Core.Models.Jobs;

public class UpdateJobResult
{
    public string JobId { get; set; }
    public bool Success { get; set; }
    public string FailureReason { get; set; }
  
}