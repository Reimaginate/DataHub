namespace Reimaginate.DataHub.SharedModels.Notifications;

public class MergeFailureNotification : Notification
{
    public MergeFailureNotification()
    {
        Subject = "MergeFailure";
        EventType = nameof(MergeFailureNotification);
    }

    public string DataSource { get; set; }
    public string AgentId { get; set; }
    public string DataHubEntityId { get; set; }
    public string DataHubEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string SourceEntityType { get; set; }
    public string FailureType { get; set; }
    public string Description { get; set; }
}
