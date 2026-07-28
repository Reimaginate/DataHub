namespace Reimaginate.DataHub.SharedModels.Notifications;

public class SyncFailureNotification : Notification
{
    public SyncFailureNotification()
    {
        Subject = "SyncFailure";
        EventType = nameof(SyncFailureNotification);
    }

    public string FailureReason { get; set; }
    public string DataHubEntityId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DataSource { get; set; }
    public string SourceEntityId { get; set; }
    public string SourceEntityType { get; set; }
}