using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Notifications;

public class CustomNotification : Notification
{
    public JObject Data { get; set; }
}