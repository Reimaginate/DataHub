using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DispatchNotifications;

public class DispatchNotificationsRequest : IRequest<DispatchNotificationsResponse>
{
    public DispatchNotificationsRequest()
    {

    }

    public DispatchNotificationsRequest(List<Notification> notifications) : this()
    {
        Notifications = notifications;
    }

    public List<Notification> Notifications { get; set; }
}