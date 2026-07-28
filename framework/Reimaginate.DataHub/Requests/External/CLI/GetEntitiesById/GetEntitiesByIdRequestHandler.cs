using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetEntitiesById;

public class GetEntitiesByIdRequestHandler(IMediator mediator) : IHandler<GetEntitiesByIdRequest, GetEntitiesResponse>
{
    public async Task<GetEntitiesResponse> HandleAsync(GetEntitiesByIdRequest request, CancellationToken cancellationToken)
    {
        var getMaterializedEntitiesResponse = (await mediator.TrySend(new GetMaterializedEntitiesByIdRequest()
        {
            EntityType = request.EntityType,
            EntityIds = request.EntityIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new GetEntitiesResponse()
        {
            Success = true,
            Results = getMaterializedEntitiesResponse.Results ?? [],
            MoreResultsAvailable = false,
            ResultCount = getMaterializedEntitiesResponse.Results?.Count ?? 0
        };
    }
}
