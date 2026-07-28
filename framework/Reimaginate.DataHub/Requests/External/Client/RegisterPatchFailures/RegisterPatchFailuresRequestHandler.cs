using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterPatchFailures;

public class RegisterPatchFailuresRequestHandler(IMediator mediator) : IHandler<RegisterPatchFailuresRequest, NullResponse>
{
    public async Task<NullResponse> HandleAsync(RegisterPatchFailuresRequest request, CancellationToken cancellationToken)
    {
        var logEventsResponse = (await mediator.SendAsync(new LogEventsRequest<PatchFailure>()
        {
            Events = request.PatchFailures
        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

        LogEventFailureHelpers.ThrowIfFailed(logEventsResponse, nameof(RegisterPatchFailuresRequest));

        var notifications = request.PatchFailures.Select(s => (Notification)new PatchFailureNotification()
        { 
            EventSource = s.EventSource,
            DataSource = s.DataSource,
            EntityId = s.EntityId,
            EntityType = s.EntityType,
            FailureReason = s.FailureReason,
            Timestamp = s.Timestamp
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
                    nameof(RegisterPatchFailuresRequest),
                    dispatchResponse.Failures?.Select(failure => failure.Exception?.Message) ?? Enumerable.Empty<string>());
            }
        }

        return new NullResponse();
    }
}
