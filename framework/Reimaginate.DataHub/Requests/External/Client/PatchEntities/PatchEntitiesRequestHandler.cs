using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.PatchEntities;

public class PatchEntitiesRequestHandler(IMediator mediator) : IHandler<PatchEntitiesRequest, PatchEntitiesResponse>
{
    public async Task<PatchEntitiesResponse> HandleAsync(PatchEntitiesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var requestsDic = request.Requests.ToDictionary(k => k, _ => Guid.NewGuid().ToString());

            var response = (await mediator.TrySend(new ProcessPatchEntitiesRequest()
            {
                CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString(),
                Requests = request.Requests.Select(r => new ProcessPatchEntityRequest()
                {
                    RequestId = requestsDic[r],
                    DataSource = r.DataSource,
                    EntityType = r.EntityType,
                    EntityId = r.EntityId,
                    Timestamp = r.Timestamp,
                    Operations = r.Operations,
                    DispatchNotifications = r.DispatchNotifications,
                    Silent = r.Silent
                }).ToList(),
                Silent = request.Silent,
                DispatchNotifications = request.DispatchNotifications,
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var results = response.Results.Select(result => new PatchEntityResponse()
            {
                Success = result.Success,
                FailureReason = result.FailureReason,
                PatchFailures = result.PatchFailures,
                PatchRequest = result.Success ? null : request.Requests.First(r => requestsDic[r] == result.RequestId)
            }).ToList();

            var failures = results.Where(w => !w.Success).ToList();
            if (failures.Any())
            {
                return new PatchEntitiesResponse()
                {
                    Success = false,
                    FailureReason = "ONE_OR_MORE_PATCHES_FAILED: " + string.Join("\n", failures.Select(s => $"{s.PatchRequest.DataSource}/{s.PatchRequest.EntityType}/{s.PatchRequest.EntityId}: {s.FailureReason}")),
                    Results = results
                };
            }

            return new PatchEntitiesResponse()
            {
                Success = true,
                Results = results
            };
        }
        catch (Exception ex)
        {
            return new PatchEntitiesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}