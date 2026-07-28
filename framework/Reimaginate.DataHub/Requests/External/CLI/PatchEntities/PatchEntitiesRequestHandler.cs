using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.PatchEntities;

public class PatchEntitiesRequestHandler(IMediator mediator) : IHandler<PatchEntitiesRequest, List<PatchEntityResponse>>
{
    public async Task<List<PatchEntityResponse>> HandleAsync(PatchEntitiesRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new ProcessPatchEntitiesRequest()
        {
            CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString(),
            Requests = request.Requests.Select(r => new ProcessPatchEntityRequest()
            {
                DataSource = r.DataSource,
                EntityType = r.EntityType,
                EntityId = r.EntityId,
                Timestamp = r.Timestamp,
                Operations = r.Operations,
                DispatchNotifications = r.DispatchNotifications,
                Silent = r.Silent,
                CommitToDb = true
            }).ToList(),
            Silent = request.Silent,
            DispatchNotifications = request.DispatchNotifications
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return response.Results.Select((s, index) =>
        {
            var patchRequest = request.Requests[index];
            return new PatchEntityResponse()
            {
                Success = s.Success,
                Changed = s.ChangeSet?.HasValues == true,
                FailureReason = s.FailureReason,
                DataSource = patchRequest.DataSource,
                EntityType = patchRequest.EntityType,
                EntityId = patchRequest.EntityId,
                PatchFailures = s.PatchFailures
            };
        }).ToList();
    }
}
