using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntity;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntity;

public class GetDataHubEntityRequestHandler(IMediator mediator) : IHandler<GetDataHubEntityRequest, GetDataHubEntityResponse>
{
    public async Task<GetDataHubEntityResponse> HandleAsync(GetDataHubEntityRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = (await mediator.TrySend(
                new GetDataHubEntityQuery()
                {
                    EntityType = request.EntityType,
                    Id = request.EntityId
                },
                cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            return new GetDataHubEntityResponse()
            {
                Success = true,
                Entity = result
            };
        }
        catch (Exception ex)
        {
            return new GetDataHubEntityResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}