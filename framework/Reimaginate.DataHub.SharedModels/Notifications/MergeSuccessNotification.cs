namespace Reimaginate.DataHub.SharedModels.Notifications;

public class MergeSuccessNotification : Notification
{
    public MergeSuccessNotification()
    {
        Subject = "MergeSuccess";
        EventType = nameof(MergeSuccessNotification);
    }

    public string DataHubEntityId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DataSource { get; set; }
    public string SourceEntityId { get; set; }
    public string SourceEntityType { get; set; }
}