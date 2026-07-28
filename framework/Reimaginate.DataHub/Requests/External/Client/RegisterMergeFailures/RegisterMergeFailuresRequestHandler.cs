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

namespace Reimaginate.DataHub.Requests.External.Client.RegisterMergeFailures;

public class RegisterMergeFailuresRequestHandler(IMediator mediator) : IHandler<RegisterMergeFailuresRequest, NullResponse>
{
    public async Task<NullResponse> HandleAsync(RegisterMergeFailuresRequest request, CancellationToken cancellationToken)
    {
        var logEventsResponse = (await mediator.SendAsync(new LogSyncEventsRequest<MergeFailure>()
        {
            SyncEvents = request.MergeFailures
        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

        LogEventFailureHelpers.ThrowIfFailed(logEventsResponse, nameof(RegisterMergeFailuresRequest));
        
        var notifications = request.MergeFailures.Select(failure => (Notification)new MergeFailureNotification()
        {
            DataSource = failure.DataSource,
            AgentId = failure.AgentId,
            DataHubEntityId = failure.DataHubEntityId,
            SourceEntityType = failure.SourceEntityType,
            DataHubEntityType = failure.DataHubEntityType,
            SourceEntityId = failure.SourceEntityId,
            FailureType = failure.FailureType,
            Description = failure.Description
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
                    nameof(RegisterMergeFailuresRequest),
                    dispatchResponse.Failures?.Select(failure => failure.Exception?.Message) ?? Enumerable.Empty<string>());
            }
        }

        return new NullResponse();
    }
}
