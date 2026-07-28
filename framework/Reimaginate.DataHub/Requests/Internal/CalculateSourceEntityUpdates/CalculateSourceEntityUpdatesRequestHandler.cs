using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.CalculateSourceEntityUpdates;

public class CalculateSourceEntityUpdatesRequestHandler(IMediator mediator, ITimeService timeService) : IHandler<CalculateSourceEntityUpdatesRequest, CalculateSourceEntityUpdatesResponse>
{
    public async Task<CalculateSourceEntityUpdatesResponse> HandleAsync(CalculateSourceEntityUpdatesRequest request, CancellationToken cancellationToken)
    {
        var resultingInitTrackedEntityRequests = new List<InitTrackedEntityRequest>();
        var resultingSourceEntityUpdates = new List<AddTrackedEntityChangeSetRequest>();
        
        #region Get the current state of source entity being tracked by the ResultingEntity Hub
        
        var trackedSourceEntity = (await mediator.TrySend(new GetTrackedEntityRequest()
        {
            DataSource = request.DataSource,
            EntityType = request.SourceEntityType,
            EntityId = request.SourceEntityId,
            AtPointInTime = request.SourceEntity.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated)),
            TrackingEntries = request.ChangeTrackingEntryCache.Where(w => w.EntityId == request.SourceEntityId).ToList()
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        #endregion

        var firstTimeMerge = trackedSourceEntity == null;
        if (firstTimeMerge)
        {
            #region Start tracking the source entity

            resultingInitTrackedEntityRequests.Add(new InitTrackedEntityRequest()
            {
                DataSource = request.DataSource,
                EntityType = request.SourceEntityType,
                EntityId = request.SourceEntityId,
                EntityData = ChangeTrackingHelper.StripBaseProperties(request.SourceEntity),
                Timestamp = request.SourceEntity?.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated))
            });

            #endregion
        }
        else
        {
            var initEntry = request.ChangeTrackingEntryCache.Last(w => w.EntityId == request.SourceEntityId && w.EntryType == "Init");
            var updateTimestamp = request.SourceEntity.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated)) ?? timeService.Now();

            if (updateTimestamp >= initEntry.Timestamp)
            {
                #region Calculate the source entity changes

                var currentData = trackedSourceEntity;
                var targetData = (JObject)trackedSourceEntity.DeepClone();

                targetData.Merge(request.SourceEntity, new JsonMergeSettings()
                {
                    MergeNullValueHandling = MergeNullValueHandling.Merge,
                    MergeArrayHandling = MergeArrayHandling.Replace
                });
                
                var entityDiffs = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(currentData.RemoveNullValues(), targetData.RemoveNullValues()));

                #endregion

                if (entityDiffs?.HasValues == true)
                {
                    #region Add a change tracking entry for the changes

                    var addChangeSetRequest = new AddTrackedEntityChangeSetRequest
                    {
                        DataSource = request.DataSource,
                        EntityType = request.SourceEntityType,
                        SourceEntityId = request.SourceEntityId,
                        ChangeSet = entityDiffs,
                        TimeStamp = updateTimestamp
                    };
                    resultingSourceEntityUpdates.Add(addChangeSetRequest);

                    request.ChangeTrackingEntryCache.Add(new ChangeTrackingEntry()
                    {
                        EntryType = ChangeTrackingEntryTypes.Update,
                        DataSource = request.DataSource,
                        EntityType = request.SourceEntityType,
                        EntityId = request.SourceEntityId,
                        Data = entityDiffs,
                        Timestamp = request.SourceEntity.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated)) ?? timeService.Now()
                    });

                    #endregion
                }
            }
        }

        return new CalculateSourceEntityUpdatesResponse()
        {
            ResultingInitTrackedEntityRequests = resultingInitTrackedEntityRequests,
            ResultingSourceEntityUpdates = resultingSourceEntityUpdates
        };
    }
}
