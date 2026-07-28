using System;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Notifications;

public class DataHubEntityUpdatedNotification : Notification
{
    public DataHubEntityUpdatedNotification()
    {
        Subject = "DataHubEntityUpdated";
        EventType = nameof(DataHubEntityUpdatedNotification);
    }
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DataHubEntityId { get; set; }
    public List<string> UpdatePaths { get; set; }
    public DateTimeOffset? SourceEventTimeStamp { get; set; }
}