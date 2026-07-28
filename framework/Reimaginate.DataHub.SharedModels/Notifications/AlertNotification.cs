namespace Reimaginate.DataHub.SharedModels.Notifications;

public class AlertNotification : Notification
{
    public AlertNotification()
    {
        Subject = "Alert";
        EventType = nameof(AlertNotification);
    }

    public string AlertId { get; set; }
    public string Severity { get; set; }
    public string AlertSubject { get; set; }
}