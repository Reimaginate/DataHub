using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.PatchEntity;

public class PatchEntityRequestHandler(IMediator mediator) : IHandler<PatchEntityRequest, PatchEntityResponse>
{
    public async Task<PatchEntityResponse> HandleAsync(PatchEntityRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new ProcessPatchEntitiesRequest()
        {
            CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString(),
            DispatchNotifications = request.DispatchNotifications,
            Silent = request.Silent,
            Requests =
            [
                new ProcessPatchEntityRequest()
                {
                    DataSource = request.DataSource,
                    EntityType = request.EntityType,
                    EntityId = request.EntityId,
                    Timestamp = request.Timestamp,
                    Operations = request.Operations,
                    DispatchNotifications = request.DispatchNotifications,
                    Silent = request.Silent
                }
            ]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var result = response.Results.First();

        return new PatchEntityResponse()
        {
            Success = result.Success,
            Changed = result.ChangeSet?.HasValues == true,
            FailureReason = result.FailureReason,
            DataSource = request.DataSource,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            PatchFailures = result.PatchFailures
        };
    }
}
