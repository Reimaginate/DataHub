using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateUntrackedEntity;

public class UpdateUntrackedEntityRequestHandler(IMediator mediator) : IHandler<UpdateUntrackedEntityRequest, UpdateUntrackedEntityResponse>
{
    public async Task<UpdateUntrackedEntityResponse> HandleAsync(UpdateUntrackedEntityRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new UpdateUntrackedEntitiesRequest
        {
            Requests = [request],
            DispatchNotifications = request.DispatchNotifications,
            Silent = request.Silent
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return response.Results.FirstOrDefault() ?? new UpdateUntrackedEntityResponse
        {
            EntityId = request.EntityId,
            EntityType = request.EntityType,
            Success = false,
            FailureReason = response.FailureReason
        };
    }
}
