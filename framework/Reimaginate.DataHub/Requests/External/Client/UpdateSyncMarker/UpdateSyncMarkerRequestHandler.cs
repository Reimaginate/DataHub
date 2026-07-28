using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateSyncMarker;

public class UpdateSyncMarkerRequestHandler(IMediator mediator) : IHandler<UpdateSyncMarkerRequest, UpdateSyncMarkerResponse>
{
    public async Task<UpdateSyncMarkerResponse> HandleAsync(UpdateSyncMarkerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            request.SyncMarker.Value = request.NewValue;
            request.SyncMarker.LastRunTime = request.RunTime;

            var upsertCommand = new UpsertCosmosDocumentsCommand<SyncMarker>()
            {
                Documents = [request.SyncMarker]
            };

            var response = (await mediator.TrySend(upsertCommand, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            return new UpdateSyncMarkerResponse()
            {
                Success = true,
                ResultingSyncMarker = response.Successes?.FirstOrDefault()
            };
        }
        catch (Exception ex)
        {
            return new UpdateSyncMarkerResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}