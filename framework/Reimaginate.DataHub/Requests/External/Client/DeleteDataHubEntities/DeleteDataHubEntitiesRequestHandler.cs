using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessDeleteDataHubEntities;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;


namespace Reimaginate.DataHub.Requests.External.Client.DeleteDataHubEntities;

public class DeleteDataHubEntitiesRequestHandler(IMediator mediator) : IHandler<DeleteDataHubEntitiesRequest, DeleteDataHubEntitiesResponse>
{
    public async Task<DeleteDataHubEntitiesResponse> HandleAsync(DeleteDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {

        var ret = (await mediator.TrySend(new ProcessDeleteDataHubEntitiesRequest()
        {
            EntityType = request.EntityType,
            EntityIds = request.EntityIds,
            IncludeTrackingEntries = request.IncludeTrackingEntries
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new DeleteDataHubEntitiesResponse()
        {
            Success = ret.Success,
            Failures = ret.Failures
        };
    }
}