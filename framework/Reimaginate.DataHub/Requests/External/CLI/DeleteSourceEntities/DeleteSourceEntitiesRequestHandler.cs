using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DeleteTrackingData;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteSourceEntities;

public class DeleteSourceEntitiesRequestHandler(IMediator mediator) : IHandler<DeleteSourceEntitiesRequest, DeleteSourceEntitiesResponse>
{
    public async Task<DeleteSourceEntitiesResponse> HandleAsync(DeleteSourceEntitiesRequest request, CancellationToken cancellationToken)
    {
        var ret = (await mediator.TrySend(new DeleteTrackingDataRequest()
        {
            DataSource = request.DataSource,
            EntityType = request.EntityType,
            EntityIds = request.EntityIds,
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new DeleteSourceEntitiesResponse()
        {
            Success = !ret.Failures.Any(),
            Failures = ret.Failures.Select(s => new DeleteSourceEntityFailure()
            {
                DataSource = s.DataSource,
                EntityType = s.EntityType,
                EntityId = s.EntityId
            }).ToList()
        };
    }
}