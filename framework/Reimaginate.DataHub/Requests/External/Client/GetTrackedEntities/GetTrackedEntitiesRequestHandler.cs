using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetTrackedEntities;

public class GetTrackedEntitiesRequestHandler(IMediator mediator) : IHandler<GetTrackedEntitiesRequest, GetTrackedEntitiesResponse>
{
    public async Task<GetTrackedEntitiesResponse> HandleAsync(GetTrackedEntitiesRequest request, CancellationToken cancellationToken)
    {
        try
        {

            #region Retrieve tracking entries for source entity

            var sourceEntityTrackingEntries = new List<ChangeTrackingEntry>();
            var getTrackingEntriesResponse = (await mediator.TrySend(new GetTrackingEntriesQuery()
            {
                DataSource = request.DataSource,
                EntityType = request.EntityType,
                EntityIds = request.EntityIds,
                PageSize = 1000
            }, CancellationToken.None)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            sourceEntityTrackingEntries.AddRange(getTrackingEntriesResponse.Results);

            while (getTrackingEntriesResponse.MoreResultsAvailable)
            {
                getTrackingEntriesResponse = (await mediator.TrySend(new GetTrackingEntriesQuery()
                {
                    DataSource = request.DataSource,
                    EntityType = request.EntityType,
                    EntityIds = request.EntityIds,
                    PageSize = 1000,
                    ContinuationToken = getTrackingEntriesResponse.ContinuationToken
                }, CancellationToken.None)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                sourceEntityTrackingEntries.AddRange(getTrackingEntriesResponse.Results);
            }

            #endregion

            var results = new ConcurrentBag<GetTrackedEntityResult>();

            await Parallel.ForEachAsync(request.EntityIds, cancellationToken, async (entityId, ct) =>
            {
                try
                {
                    var trackedEntity = (await mediator.TrySend(new GetTrackedEntityRequest()
                    {
                        DataSource = request.DataSource,
                        EntityType = request.EntityType,
                        EntityId = entityId,
                        TrackingEntries = sourceEntityTrackingEntries.Where(w => w.EntityId == entityId).ToList()

                    }, ct)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    results.Add(new GetTrackedEntityResult()
                    {
                        Success = true,
                        DataSource = request.DataSource,
                        EntityType = request.EntityType,
                        EntityId = entityId,
                        Data = trackedEntity
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new GetTrackedEntityResult()
                    {
                        Success = false,
                        FailureReason = ex.Message,
                        DataSource = request.DataSource,
                        EntityType = request.EntityType,
                        EntityId = entityId,
                        Data = null
                    });
                }
            });

            return new GetTrackedEntitiesResponse()
            {
                Success = true,
                Results = results.ToList()
            };
        }
        catch (Exception ex)
        {
            return new GetTrackedEntitiesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}