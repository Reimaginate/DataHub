using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.PatchSyncMarker;

public class PatchSyncMarkerRequestHandler(IMediator mediator) : IHandler<PatchSyncMarkerRequest, PatchSyncMarkerResponse>
{
    private const string PatchedStatus = "Patched";
    private const string DryRunStatus = "DryRun";
    private const string UnchangedStatus = "Unchanged";
    private const string FailedStatus = "Failed";

    public async Task<PatchSyncMarkerResponse> HandleAsync(PatchSyncMarkerRequest request, CancellationToken cancellationToken)
    {
        var marker = await LoadMarker(request.MarkerId, cancellationToken);
        if (marker == null)
        {
            return new PatchSyncMarkerResponse
            {
                MarkerId = request.MarkerId,
                Success = false,
                Status = FailedStatus,
                Reason = "Sync marker was not found."
            };
        }

        var updatedMarker = new SyncMarker
        {
            id = marker.id,
            _dt = marker._dt,
            _etag = marker._etag,
            _ts = marker._ts,
            AgentId = marker.AgentId,
            DataSource = marker.DataSource,
            EntityType = marker.EntityType,
            Value = marker.Value,
            LastRunTime = marker.LastRunTime
        };

        if (request.UpdateValue)
        {
            updatedMarker.Value = request.Value;
        }

        if (request.UpdateLastRunTime)
        {
            updatedMarker.LastRunTime = request.LastRunTime;
        }

        var changed = marker.Value != updatedMarker.Value || marker.LastRunTime != updatedMarker.LastRunTime;
        if (changed && !request.DryRun)
        {
            var upsertResponse = (await mediator.TrySend(new UpsertCosmosDocumentsCommand<SyncMarker>
            {
                Documents = [updatedMarker]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var failure = upsertResponse.Failures?.FirstOrDefault();
            if (failure != null)
            {
                return new PatchSyncMarkerResponse
                {
                    MarkerId = request.MarkerId,
                    Success = false,
                    Changed = changed,
                    Status = FailedStatus,
                    Reason = failure.Error?.Message,
                    Result = updatedMarker
                };
            }

            updatedMarker = upsertResponse.Successes?.FirstOrDefault() ?? updatedMarker;
        }

        return new PatchSyncMarkerResponse
        {
            MarkerId = request.MarkerId,
            Success = true,
            Changed = changed,
            Status = request.DryRun ? DryRunStatus : changed ? PatchedStatus : UnchangedStatus,
            Reason = request.DryRun ? "Matched but not patched." : changed ? "Patch applied." : "Patch made no changes.",
            Result = updatedMarker
        };
    }

    private async Task<SyncMarker> LoadMarker(string markerId, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<SyncMarker>
        {
            WhereClause = $"x.{nameof(SyncMarker._dt)} = @documentType and x.{nameof(SyncMarker.id)} = @markerId",
            Parameters =
            [
                new QueryParameter("documentType", nameof(SyncMarker)),
                new QueryParameter("markerId", markerId)
            ],
            PageSize = 1
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return response.Results?.FirstOrDefault();
    }
}
