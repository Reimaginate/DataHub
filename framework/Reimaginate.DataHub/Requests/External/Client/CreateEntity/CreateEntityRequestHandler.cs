using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.CreateEntity;

public class CreateEntityRequestHandler(IMediator mediator) : IHandler<CreateEntityRequest, CreateEntityResponse>
{
    public async Task<CreateEntityResponse> HandleAsync(CreateEntityRequest request, CancellationToken cancellationToken)
    {
        request.Data[nameof(DataHubEntity.id)] = request.EntityId;

        var response = (await mediator.TrySend(new CreateEntitiesRequest()
        {
            Requests = [request]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return response.Results.FirstOrDefault();
    }
}