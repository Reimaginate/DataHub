using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.DeleteDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.DeleteTrackingData;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteDataHubEntities;

public class ProcessDeleteDataHubEntitiesRequestHandler(IMediator mediator) : IHandler<ProcessDeleteDataHubEntitiesRequest, ProcessDeleteDataHubEntitiesResponse>
{
    public async Task<ProcessDeleteDataHubEntitiesResponse> HandleAsync(ProcessDeleteDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<DeleteDataHubEntityFailure>();

        #region Retrieve ResultingEntity Hub Entities

        var entitiesToDelete = request.Entities ?? ((await mediator.TrySend(new GetDataHubEntitiesByIdRequest()
        {
            EntityType = request.EntityType,
            EntityIds = request.EntityIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue }).Results;

        #endregion

        var notFoundIds = request.EntityIds.Except(entitiesToDelete.Select(s => s.DataHubEntityId()));
        foreach (var notFoundId in notFoundIds)
        {
            failures.Add(new DeleteDataHubEntityFailure()
            {
                DataHubEntity = new JObject() { { nameof(DataHubEntity.id), notFoundId } },
                FailureReason = "Key not found"
            });
        }

        if (entitiesToDelete.Any())
        {
            var deleteEntitiesResponse = (await mediator.TrySend(new DeleteDataHubEntitiesCommand()
            {
                Entities = entitiesToDelete
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            failures.AddRange(deleteEntitiesResponse.Failures.Select(s => new DeleteDataHubEntityFailure()
            {
                DataHubEntity = s.Item,
                FailureReason = s.Error?.Message
            }).ToList());

            if (request.IncludeTrackingEntries)
            {
                var entitiesToDeleteTrackingFor = entitiesToDelete
                    .Select(s => s.DataHubEntityId())
                    .Except(deleteEntitiesResponse.Failures.Select(s => s.Item.DataHubEntityId()))
                    .ToList();

                var deleteTrackedEntitiesResponse = (await mediator.TrySend(new DeleteTrackingDataRequest()
                {
                    DataSource = "DataHub",
                    EntityType = request.EntityType,
                    EntityIds = entitiesToDeleteTrackingFor
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                failures.AddRange(deleteTrackedEntitiesResponse.Failures.Select(s =>
                {
                    var dataHubEntity = entitiesToDelete.First(f => f.DataHubEntityId() == s.EntityId);
                    return new DeleteDataHubEntityFailure()
                    {
                        DataHubEntity = dataHubEntity,
                        FailureReason = $"Failed to delete tracking entries: {s.Exception?.Message}"
                    };
                }));
            }
        }

        return new ProcessDeleteDataHubEntitiesResponse()
        {
            Success = !failures.Any(),
            Failures = failures
        };
    }
}