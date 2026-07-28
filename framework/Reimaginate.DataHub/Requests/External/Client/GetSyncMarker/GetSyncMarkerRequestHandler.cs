using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetSyncMarkers;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetSyncMarker;

public class GetSyncMarkerRequestHandler(IIdService idService, IMediator mediator) : IHandler<GetSyncMarkerRequest, GetSyncMarkerResponse>
{
    public async Task<GetSyncMarkerResponse> HandleAsync(GetSyncMarkerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = (await mediator.TrySend(new GetSyncMarkersQuery()
            {
                DataSource = request.DataSource,
                EntityType = request.DataHubEntityType
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };


            var syncMarker = response.MaxBy(o => o._ts);
            if (response.Count > 1)
            {
                try
                {
                    var mergeMarkersToDelete = response.Except(new List<SyncMarker>() { syncMarker }).ToList();
                    _ = (await mediator.SendAsync(new DeleteCosmosDocumentsCommand<SyncMarker>(mergeMarkersToDelete), cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
                }
                catch (Exception)
                {
                    //ignore

                }
            }

            return new GetSyncMarkerResponse()
            {
                Success = true,
                SyncMarker = syncMarker ?? new SyncMarker()
                {
                    id = idService.NewId<SyncMarker>(),
                    EntityType = request.DataHubEntityType,
                    DataSource = request.DataSource,
                    AgentId = request.AgentId,
                    Value = request.DefaultValue
                }
            };
        }
        catch (Exception ex)
        {
            return new GetSyncMarkerResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
