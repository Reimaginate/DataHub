using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessRevertDataHubEntities;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.RevertDataHubEntities;

public class RevertDataHubEntitiesRequestHandler(IMediator mediator) : IHandler<RevertDataHubEntitiesRequest, RevertDataHubEntitiesResponse>
{
    public async Task<RevertDataHubEntitiesResponse> HandleAsync(RevertDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new ProcessRevertDataHubEntitiesRequest
        {
            CorrelationId = request.CorrelationId,
            EntityType = request.EntityType,
            EntityIds = request.EntityIds,
            RevertTo = request.RevertTo,
            TrackingEntryId = request.TrackingEntryId,
            DispatchNotifications = request.DispatchNotifications,
            DryRun = request.DryRun
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new RevertDataHubEntitiesResponse
        {
            Results = response.Results
        };
    }
}
