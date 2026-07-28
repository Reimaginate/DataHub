using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;
using InternalDetachEntitiesFromDataSourceRequest = Reimaginate.DataHub.Requests.Internal.DetachEntitiesFromDataSource.DetachEntitiesFromDataSourceRequest;
using InternalDetachEntitiesFromDataSourceResponse = Reimaginate.DataHub.Requests.Internal.DetachEntitiesFromDataSource.DetachEntitiesFromDataSourceResponse;

namespace Reimaginate.DataHub.Requests.External.Client.DetachEntitiesFromDataSource;

public class DetachEntitiesFromDataSourceRequestHandler(IMediator mediator) : IHandler<DetachEntitiesFromDataSourceRequest, DetachEntitiesFromDataSourceResponse>
{
    public async Task<DetachEntitiesFromDataSourceResponse> HandleAsync(DetachEntitiesFromDataSourceRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new InternalDetachEntitiesFromDataSourceRequest()
        {
            DataSource = request.DataSource,
            EntityType = request.EntityType,
            EntityIds = request.EntityIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var failures = response.Failures?.Select(failure => failure.Message).ToList() ?? [];

        return new DetachEntitiesFromDataSourceResponse()
        {
            Success = !failures.Any(),
            FailureReason = failures.Any() ? "ONE_OR_MORE_DETACHES_FAILED" : null,
            Failures = failures
        };
    }
}
