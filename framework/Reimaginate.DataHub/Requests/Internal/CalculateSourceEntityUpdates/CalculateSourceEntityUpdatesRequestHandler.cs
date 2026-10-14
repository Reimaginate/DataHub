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
        // Validate every input, including unchanged keys, before advancing the source cache.
        MergeBatchIntegrity.ValidateAlternateKeys(request.SourceEntity);
        var resultingInitTrackedEntityRequests = new List<InitTrackedEntityRequest>();
        var resultingSourceEntityUpdates = new List<AddTrackedEntityChangeSetRequest>();
        var trackingEntries = request.OrderedChangeTrackingEntryCache ?? request.ChangeTrackingEntryCache.ToList();
        
        #region Get the current state of source entity being tracked by the ResultingEntity Hub
        
        var trackedSourceEntity = (await mediator.TrySend(new GetTrackedEntityRequest()
        {
            DataSource = request.DataSource,
            EntityType = request.SourceEntityType,
            EntityId = request.SourceEntityId,
            AtPointInTime = request.SourceEntity.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated)),
            TrackingEntries = trackingEntries.Where(w => w.EntityId == request.SourceEntityId).ToList()
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
            var initEntry = trackingEntries.Last(w => w.EntityId == request.SourceEntityId && w.EntryType == "Init");
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

                if (entityDiffs?.HasValues == true || request.RetainUnchangedUpdate)
                {
                    #region Add a change tracking entry for the changes

                    var addChangeSetRequest = new AddTrackedEntityChangeSetRequest
                    {
                        DataSource = request.DataSource,
                        EntityType = request.SourceEntityType,
                        SourceEntityId = request.SourceEntityId,
                        ChangeSet = entityDiffs ?? new JObject(),
                        TimeStamp = updateTimestamp,
                        SourceEntityInput = request.SourceEntity,
                        SourceEntityAlternateKeys = request.SourceEntity[nameof(DataHubEntity.alternateKeys)] is JArray alternateKeys
                            ? (JArray)alternateKeys.DeepClone()
                            : null
                    };
                    resultingSourceEntityUpdates.Add(addChangeSetRequest);

                    var trackingEntry = new ChangeTrackingEntry()
                    {
                        EntryType = ChangeTrackingEntryTypes.Update,
                        DataSource = request.DataSource,
                        EntityType = request.SourceEntityType,
                        EntityId = request.SourceEntityId,
                        Data = entityDiffs,
                        Timestamp = updateTimestamp
                    };
                    if (entityDiffs?.HasValues == true)
                    {
                        if (request.OrderedChangeTrackingEntryCache != null)
                            request.OrderedChangeTrackingEntryCache.Add(trackingEntry);
                        else
                            request.ChangeTrackingEntryCache.Add(trackingEntry);
                    }

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
