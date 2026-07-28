using System;

namespace Reimaginate.DataHub.SharedModels.Notifications;

public class DataHubEntityCreatedNotification : Notification
{
    public DataHubEntityCreatedNotification()
    {
        Subject = "DataHubEntityCreated";
        EventType = nameof(DataHubEntityCreatedNotification);
    }
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DataHubEntityId { get; set; }
    public DateTimeOffset? SourceEventTimeStamp { get; set; }
}