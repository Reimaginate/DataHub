using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetMergeMarkers;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetMergeMarker;

public class GetMergeMarkerRequestHandler(IIdService idService, IMediator mediator) : IHandler<GetMergeMarkerRequest, GetMergeMarkerResponse>
{
    public async Task<GetMergeMarkerResponse> HandleAsync(GetMergeMarkerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = (await mediator.TrySend(new GetMergeMarkersQuery()
            {
                DataSource = request.DataSource,
                EntityType = request.SourceEntityType
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var mergeMarker = response.MaxBy(o => o._ts);
            if (response.Count > 1)
            {
                try
                {
                    var mergeMarkersToDelete = response.Except(new List<MergeMarker>() { mergeMarker }).ToList();
                    _ = (await mediator.SendAsync(new DeleteCosmosDocumentsCommand<MergeMarker>(mergeMarkersToDelete), cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
                }
                catch (Exception)
                {
                    //ignore
                }
            }

            return new GetMergeMarkerResponse()
            {
                Success = true,
                MergeMarker = mergeMarker ?? new MergeMarker()
                {
                    id = idService.NewId<MergeMarker>(),
                    EntityType = request.SourceEntityType,
                    DataSource = request.DataSource,
                    AgentId = request.AgentId,
                    Value = request.DefaultValue
                }
            };
        }
        catch (Exception ex)
        {
            return new GetMergeMarkerResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}