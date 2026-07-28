using System.Collections.Generic;

namespace Reimaginate.DataHub.Requests.Internal.DispatchNotifications;

public class DispatchNotificationsResponse
{
    public bool Success { get; set; }
    public List<DispatchNotificationFailure> Failures { get; set; }
}