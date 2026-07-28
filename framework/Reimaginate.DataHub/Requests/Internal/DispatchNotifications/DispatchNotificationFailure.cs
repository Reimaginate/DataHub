using System;
using Reimaginate.DataHub.SharedModels.Notifications;

namespace Reimaginate.DataHub.Requests.Internal.DispatchNotifications;

public class DispatchNotificationFailure(Notification notification, Exception exception)
{
    public Notification Notification { get; set; } = notification;
    public Exception Exception { get; set; } = exception;
}