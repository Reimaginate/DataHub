using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.UpdateTrackedEntity;

public class UpdateTrackedEntityRequestHandler(IMediator mediator, ITimeService timeService) : IHandler<UpdateTrackedEntityRequest, UpdateTrackedEntityResponse>
{
    public async Task<UpdateTrackedEntityResponse> HandleAsync(UpdateTrackedEntityRequest request, CancellationToken cancellationToken)
    {
        var retrieveTrackedEntityRequest = new GetTrackedEntityRequest
        {
            EntityId = request.SourceEntityId,
            DataSource = request.DataSource,
            EntityType = request.EntityType,
            AtPointInTime = request.EntityData[nameof(DataHubEntity.lastUpdated)]?.AsDateTimeOffset() ?? timeService.Now(),
            TrackingEntries = request.TrackingEntries.Where(w => w.EntityType == request.EntityType && w.EntityId == request.SourceEntityId).ToList()
        };

        var trackedEntity = (await mediator.TrySend(retrieveTrackedEntityRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (trackedEntity == null)
        {
            var trackEntryRequest = new InitTrackedEntityRequest()
            {
                EntityType = request.EntityType,
                EntityData = ChangeTrackingHelper.StripBaseProperties(request.EntityData),
                EntityId = request.SourceEntityId,
                DataSource = request.DataSource
            };

            var changeTrackingEntry = (await mediator.TrySend(trackEntryRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            trackedEntity = request.EntityData;
            trackedEntity[nameof(DataHubEntity.id)] = request.SourceEntityId;
            trackedEntity[nameof(DataHubEntity.entityType)] = request.EntityType;
            trackedEntity[nameof(DataHubEntity.lastUpdated)] = changeTrackingEntry.Timestamp;
        }

        var entityDiffs = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(JObject.FromObject(trackedEntity), JObject.FromObject(request.EntityData)));
        if (entityDiffs?.HasValues == true)
        {
            if (!request.SkipSave)
            {
                var addChangeSetRequest = new AddTrackedEntityChangeSetRequest
                {
                    DataSource = request.DataSource,
                    EntityType = request.EntityType,
                    SourceEntityId = request.SourceEntityId,
                    ChangeSet = entityDiffs,
                    TimeStamp = request.EntityData.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated)) ??
                                timeService.Now()
                };

                _ = (await mediator.SendAsync(addChangeSetRequest, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

                trackedEntity =
                    _ = (await mediator.TrySend(retrieveTrackedEntityRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            }

            return new UpdateTrackedEntityResponse()
            {
                EntityCurrentState = trackedEntity,
                Updates = entityDiffs is { HasValues: true } ? entityDiffs : null
            };
        }

        return new UpdateTrackedEntityResponse()
        {
            EntityCurrentState = trackedEntity,
            Updates = entityDiffs is { HasValues: true } ? entityDiffs : null
        };
    }
}
