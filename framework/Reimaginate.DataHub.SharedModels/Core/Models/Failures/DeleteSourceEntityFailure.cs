namespace Reimaginate.DataHub.SharedModels.Core.Models.Failures;

public class DeleteSourceEntityFailure
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string FailureReason { get; set; }

}