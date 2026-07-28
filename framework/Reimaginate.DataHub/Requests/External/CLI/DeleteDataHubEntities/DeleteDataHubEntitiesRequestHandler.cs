using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessDeleteDataHubEntities;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteDataHubEntities;

public class DeleteDataHubEntitiesRequestHandler(IMediator mediator) : IHandler<SharedModels.Requests.CLI.DeleteDataHubEntitiesRequest, SharedModels.Requests.CLI.DeleteDataHubEntitiesResponse>
{
    public async Task<SharedModels.Requests.CLI.DeleteDataHubEntitiesResponse> HandleAsync(SharedModels.Requests.CLI.DeleteDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {
        var ret = (await mediator.TrySend(new ProcessDeleteDataHubEntitiesRequest()
        {
            EntityType = request.EntityType,
            EntityIds = request.EntityIds,
            IncludeTrackingEntries = request.IncludeTrackingEntries
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new SharedModels.Requests.CLI.DeleteDataHubEntitiesResponse()
        {
            Success = ret.Success,
            Failures = ret.Failures
        };
    }
}