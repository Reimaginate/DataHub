using System;

namespace Reimaginate.DataHub.SharedModels.Notifications;

public class PatchFailureNotification : Notification
{
    public PatchFailureNotification()
    {
        Subject = "PatchFailure";
        EventType = nameof(PatchFailureNotification);
    }
    public DateTimeOffset Timestamp { get; set; }
    public string EventSource { get; set; }
    public string DataSource { get; set; }
    public string EntityId { get; set; }
    public string EntityType { get; set; }
    public string FailureReason { get; set; }
}