using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterAlerts;

public class RegisterAlertsRequestHandler(IMediator mediator) : IHandler<RegisterAlertsRequest, NullResponse>
{
    public async Task<NullResponse> HandleAsync(RegisterAlertsRequest request, CancellationToken cancellationToken)
    {
        var logEventsResponse = (await mediator.TrySend(new LogEventsRequest<Alert>()
        {
            Events = request.Alerts
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        LogEventFailureHelpers.ThrowIfFailed(logEventsResponse, nameof(RegisterAlertsRequest));

        var notifications = logEventsResponse.LogEntries.Select(s =>
        {
            var alert = s.Data.ToObject<Alert>();

            return (Notification)new AlertNotification()
            {
                AlertId = s.id,
                Severity = alert.Severity,
                AlertSubject = alert.Subject,
            };
        }).ToList();

        if (notifications.Any())
        {
            var dispatchResponse = (await mediator.SendAsync(new DispatchNotificationsRequest()
            {
                Notifications = notifications
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

            if (!dispatchResponse.Success)
            {
                throw LogEventFailureHelpers.CreateNotificationDispatchException(
                    nameof(RegisterAlertsRequest),
                    dispatchResponse.Failures?.Select(failure => failure.Exception?.Message) ?? Enumerable.Empty<string>());
            }
        }

        return new NullResponse();
    }
}
