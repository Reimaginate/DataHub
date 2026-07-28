namespace Reimaginate.DataHub.SharedModels.Core.Models.Failures;

public class PatchFailure
{
    public string FailureReason { get; set; }
    public Patch Patch { get; set; }
}