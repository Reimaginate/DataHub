using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;

using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterSyncSuccesses;

public class RegisterSyncSuccessesRequestHandler(IMediator mediator) : IHandler<RegisterSyncSuccessesRequest, NullResponse>
{
    public async Task<NullResponse> HandleAsync(RegisterSyncSuccessesRequest request, CancellationToken cancellationToken)
    {
        _ = (await mediator.SendAsync(new LogSyncEventsRequest<SyncSuccess>()
        {
            SyncEvents = request.SyncSuccesses
        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
        
        var notifications = request.SyncSuccesses.Select(s => (Notification) new SyncSuccessNotification()
        {
            DataSource = s.DataSource,
            DataHubEntityId = s.DataHubEntityId,
            SourceEntityType = s.SourceEntityType,
            DataHubEntityType = s.DataHubEntityType,
            SourceEntityId = s.SourceEntityId
        }).ToList();

        if (notifications.Any())
        {
            _ = (await mediator.SendAsync(new DispatchNotificationsRequest()
            {
                Notifications = notifications
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
        }

        return new NullResponse();
    }
}