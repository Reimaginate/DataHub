using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.DetachEntityFromDataSource;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.UpdateTrackedEntity;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DetachEntitiesFromDataSource;

public class DetachEntitiesFromDataSourceRequestHandler(IMediator mediator) : IHandler<DetachEntitiesFromDataSourceRequest, DetachEntitiesFromDataSourceResponse>
{
    public async Task<DetachEntitiesFromDataSourceResponse> HandleAsync(DetachEntitiesFromDataSourceRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();

        var whereClauses = new List<string>
        {
            $"x.{nameof(DataHubEntity.entityType)} = '{request.EntityType}'",
            $"ARRAY_CONTAINS([{string.Join(",", request.EntityIds.Select(s => $"\"{s}\""))}], x.id)"
        };
        var where = string.Join(" and ", whereClauses);

        var getEntitiesResponse = (await mediator.TrySend(new GetDataHubEntitiesQuery()
        {
            WhereClause = where,
            PageSize = request.EntityIds.Count
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var getTrackingEntriesResponse = (await mediator.TrySend(
            new GetAllTrackingEntriesForEntitiesRequest()
            {
                DataSource = "DataHub",
                EntityType = request.EntityType,
                EntityIds = request.EntityIds
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var resultingEntities = new ConcurrentBag<JObject>();

        await Parallel.ForEachAsync(getEntitiesResponse.Results, new ParallelOptions()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 10
        }, async (entity, ct) =>
        {
            var detachEntityResponse = (await mediator.TrySend(new DetachEntityFromDataSourceRequest()
            {
                DataSource = request.DataSource,
                EntityType = entity.Value<string>(nameof(DataHubEntity.entityType)),
                EntityId = entity.DataHubEntityId(),
                TrackingEntries = getTrackingEntriesResponse.TrackingEntries.Where(w => w.EntityType == entity.Value<string>(nameof(DataHubEntity.entityType)) && w.EntityId == entity.DataHubEntityId()).ToList(),
                SkipSave = true

            }, ct, ex => failures.Add(ex))) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (detachEntityResponse.ResultingEntity != null) resultingEntities.Add(detachEntityResponse.ResultingEntity);
        });

        if (resultingEntities.Any())
        {
            var updateTrackedEntitiesRequests = resultingEntities.Select(s => new UpdateTrackedEntityRequest()
            {
                DataSource = "DataHub",
                EntityType = s.Value<string>(nameof(DataHubEntity.entityType)),
                SourceEntityId = s.DataHubEntityId(),
                EntityData = s,
                TrackingEntries = getTrackingEntriesResponse.TrackingEntries,
                SkipSave = true
            }).ToList();

            var changeSetRequests = new ConcurrentBag<AddTrackedEntityChangeSetRequest>();
            var updatedEntities = new ConcurrentBag<JObject>();

            await Parallel.ForEachAsync(updateTrackedEntitiesRequests, new ParallelOptions()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 10
            }, async (updateTrackedEntityRequest, ct) =>
            {
                var updateTrackedEntityResponse = (await mediator.TrySend(updateTrackedEntityRequest, ct, ex => failures.Add(ex))) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                
                if (updateTrackedEntityResponse.Updates != null)
                {
                    changeSetRequests.Add(new AddTrackedEntityChangeSetRequest()
                    {
                        DataSource = "DataHub",
                        EntityType = updateTrackedEntityRequest.EntityType,
                        SourceEntityId = updateTrackedEntityRequest.SourceEntityId,
                        ChangeSet = updateTrackedEntityResponse.Updates,
                        TimeStamp = updateTrackedEntityRequest.EntityData.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated)) ?? DateTimeOffset.UtcNow
                    });

                    updatedEntities.Add(updateTrackedEntityRequest.EntityData);
                }
            });

            _ = (await mediator.SendAsync(new AddTrackedEntityChangeSetsRequest()
            {
                Requests = changeSetRequests.ToList()
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

            var upsertDataHubEntitiesResponse = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
            {
                Entities = updatedEntities.ToList().Select(s => s.RemoveNullValues()).ToList()
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        }


        while (getEntitiesResponse.MoreResultsAvailable)
        {
            getEntitiesResponse = (await mediator.TrySend(new GetDataHubEntitiesQuery()
            {
                ContinuationToken = getEntitiesResponse.ContinuationToken,
                WhereClause = where,
                PageSize = request.EntityIds.Count
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            foreach (var entity in getEntitiesResponse.Results)
            {
                _ = await mediator.TrySend(new DetachEntityFromDataSourceRequest()
                {
                    DataSource = request.DataSource,
                    EntityType = entity.Value<string>(nameof(DataHubEntity.entityType)),
                    EntityId = entity.DataHubEntityId()

                }, cancellationToken, ex => failures.Add(ex));
            }
        }


        return new DetachEntitiesFromDataSourceResponse()
        {
            Failures = failures
        };
    }
}
