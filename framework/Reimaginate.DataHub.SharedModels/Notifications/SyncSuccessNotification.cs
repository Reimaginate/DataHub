namespace Reimaginate.DataHub.SharedModels.Notifications;

public class SyncSuccessNotification : Notification
{
    public SyncSuccessNotification()
    {
        Subject = "SyncSuccess";
        EventType = nameof(SyncSuccessNotification);
    }
    public string DataHubEntityId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DataSource { get; set; }
    public string SourceEntityId { get; set; }
    public string SourceEntityType { get; set; }
}