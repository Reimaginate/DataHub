namespace Reimaginate.DataHub.SharedModels.Core.Models.Failures;

public class DeleteLogFailure
{
    public string LogEntryId { get; set; }
    public string FailureReason { get; set; }
}