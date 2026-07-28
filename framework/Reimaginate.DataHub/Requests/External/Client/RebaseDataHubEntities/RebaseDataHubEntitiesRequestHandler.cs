using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.RebaseDataHubEntities;

public class RebaseDataHubEntitiesRequestHandler(IMediator mediator) : IHandler<RebaseDataHubEntitiesRequest, RebaseDataHubEntitiesResponse>
{
    public async Task<RebaseDataHubEntitiesResponse> HandleAsync(RebaseDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {
        var results = new List<RebaseEntityTrackingResult>();
        
        var resetChangeTrackingResponse = (await mediator.TrySend(new ProcessRebaseDataHubEntitiesRequest()
        {
            EntityType = request.EntityType,
            EntityIds = request.EntityIds,
            RebaseTo = request.RebaseTo
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        resetChangeTrackingResponse.Results.ForEach(result => results.Add(new RebaseEntityTrackingResult()
        {
            DataSource = result.DataSource,
            EntityType = result.EntityType,
            EntityId = result.EntityId,
            RebasedTo = request.RebaseTo,
            Success = result.Success,
            FailureReason = result.FailureReason,
            ArchivedTrackingEntries = result.ArchivedTrackingEntries
        }));

        return new RebaseDataHubEntitiesResponse()
        {
            Results = results
        };
    }
}