using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;

using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterSyncFailures;

public class RegisterSyncFailuresRequestHandler(IMediator mediator) : IHandler<RegisterSyncFailuresRequest, NullResponse>
{
    public async Task<NullResponse> HandleAsync(RegisterSyncFailuresRequest request, CancellationToken cancellationToken)
    {
        var logEventsResponse = (await mediator.SendAsync(new LogSyncEventsRequest<SyncFailure>()
        {
            SyncEvents = request.SyncFailures
        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

        LogEventFailureHelpers.ThrowIfFailed(logEventsResponse, nameof(RegisterSyncFailuresRequest));
        
        var notifications = request.SyncFailures.Select(s => (Notification)new SyncFailureNotification()
        {
            DataSource = s.DataSource,
            DataHubEntityId = s.DataHubEntityId,
            SourceEntityType = s.SourceEntityType,
            DataHubEntityType = s.DataHubEntityType,
            SourceEntityId = s.SourceEntityId,
            FailureReason = s.Description
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
                    nameof(RegisterSyncFailuresRequest),
                    dispatchResponse.Failures?.Select(failure => failure.Exception?.Message) ?? Enumerable.Empty<string>());
            }
        }

        return new NullResponse();
    }
}
