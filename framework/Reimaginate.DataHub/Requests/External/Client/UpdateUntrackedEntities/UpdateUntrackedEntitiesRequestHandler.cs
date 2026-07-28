using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateUntrackedEntities;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateUntrackedEntities;

public class UpdateUntrackedEntitiesRequestHandler(IMediator mediator) : IHandler<UpdateUntrackedEntitiesRequest, UpdateUntrackedEntitiesResponse>
{
    public async Task<UpdateUntrackedEntitiesResponse> HandleAsync(UpdateUntrackedEntitiesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var processUpdateEntitiesRequest = new ProcessUpdateUntrackedEntitiesRequest()
            {
                Requests = request.Requests.Select(s => new ProcessUpdateUntrackedEntityRequest()
                {
                    CreateIfMissing = s.CreateIfMissing,
                    Data = s.Data,
                    EntityId = s.EntityId,
                    EntityType = s.EntityType,
                    Silent = request.Silent || s.Silent,
                    DispatchNotifications = request.DispatchNotifications || s.DispatchNotifications
                }).ToList(),
                Silent = request.Silent,
                DispatchNotifications = request.DispatchNotifications
            };

            var processUpdateEntitiesResponse = (await mediator.TrySend(processUpdateEntitiesRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            var results = processUpdateEntitiesResponse.Results.Select(s => new UpdateUntrackedEntityResponse()
            {
                EntityId = s.EntityId,
                EntityType = s.EntityType,
                FailureReason = s.FailureReason,
                Success = s.Success
            }).ToList();

            var failures = results.Where(w => !w.Success).ToList();
            return new UpdateUntrackedEntitiesResponse()
            {
                Success = !failures.Any(),
                FailureReason = failures.Any() ? $"ONE_OR_MORE_UPDATES_FAILED: {string.Join("\n", failures.Select(f => $"{f.EntityType}/{f.EntityId}: {f.FailureReason}"))}" : null,
                Results = results
            };
        }
        catch (Exception ex)
        {
            return new UpdateUntrackedEntitiesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
