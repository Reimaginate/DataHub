using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.PatchEntity;

public class PatchEntityRequestHandler(IMediator mediator) : IHandler<PatchEntityRequest, PatchEntityResponse>
{
    public async Task<PatchEntityResponse> HandleAsync(PatchEntityRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = (await mediator.TrySend(new ProcessPatchEntitiesRequest()
            {
                CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString(),
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
                ],
                Silent = request.Silent,
                DispatchNotifications = request.DispatchNotifications,
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var result = response.Results.First();

            if (!result.Success)
            {
                return new PatchEntityResponse()
                {
                    Success = false,
                    FailureReason = result.FailureReason,
                    PatchFailures = result.PatchFailures,
                    PatchRequest = request
                };
            }

            return new PatchEntityResponse()
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new PatchEntityResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}