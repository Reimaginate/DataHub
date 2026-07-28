using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateMergeMarker;

public class UpdateMergeMarkerRequestHandler(IMediator mediator) : IHandler<UpdateMergeMarkerRequest, UpdateMergeMarkerResponse>
{
    public async Task<UpdateMergeMarkerResponse> HandleAsync(UpdateMergeMarkerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            request.MergeMarker.Value = request.NewValue;
            request.MergeMarker.LastRunTime = request.RunTime;

            var upsertCommand = new UpsertCosmosDocumentsCommand<MergeMarker>()
            {
                Documents = new List<MergeMarker>() { request.MergeMarker }
            };

            var response = (await mediator.TrySend(upsertCommand, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            if (response.Failures.Any()) throw new AggregateException(response.Failures.Select(s => s.Error));

            return new UpdateMergeMarkerResponse()
            {
                Success = true,
                ResultingMergeMarker = response.Successes?.FirstOrDefault()
            };
        }
        catch (Exception ex)
        {
            return new UpdateMergeMarkerResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}