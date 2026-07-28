using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;
using Reimaginate.DataHub.Requests.Internal.UpdateTrackedEntity;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DetachEntityFromDataSource;

public class DetachEntityFromDataSourceHandler(IMediator mediator) : IHandler<DetachEntityFromDataSourceRequest, DetachEntityFromDataSourceResponse>
{
    public async Task<DetachEntityFromDataSourceResponse> HandleAsync(DetachEntityFromDataSourceRequest request, CancellationToken cancellationToken)
    {
        var trackedEntity = (await mediator.TrySend(new GetTrackedEntityRequest()
        {
            DataSource = DataSources.DataHub,
            EntityType = request.EntityType,
            TrackingEntries = request.TrackingEntries?.Where(w => w.EntityType == request.EntityType && w.EntityId == request.EntityId).ToList(),
            EntityId = request.EntityId
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (trackedEntity[nameof(DataHubEntity.alternateKeys)]?.HasValues ?? false)
        {
            var trackedEntityAlternateKeys = trackedEntity[nameof(DataHubEntity.alternateKeys)]?.ToObject<List<AlternateKey>>();
            var dataSourceAltKeys = trackedEntityAlternateKeys?.Where(w => string.Equals(w.Key, request.DataSource, StringComparison.CurrentCultureIgnoreCase)
                                                                           || w.Key.ToLower().StartsWith($"{request.DataSource.ToLower()}.")).ToList();

            if (dataSourceAltKeys?.Any() ?? false)
            {
                var resultingAltKeys = trackedEntityAlternateKeys!.Except(dataSourceAltKeys!);
                trackedEntity[nameof(DataHubEntity.alternateKeys)] = JArray.FromObject(resultingAltKeys!);

                if (request.SkipSave) return new DetachEntityFromDataSourceResponse()
                {
                    ResultingEntity = trackedEntity
                };

                _ = (await mediator.TrySend(new UpdateTrackedEntityRequest()
                {
                    DataSource = DataSources.DataHub,
                    EntityType = request.EntityType,
                    SourceEntityId = request.EntityId,
                    EntityData = trackedEntity
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                _ = (await mediator.SendAsync(new MaterializeDataHubEntityRequest()
                {
                    EntityType = request.EntityType,
                    EntityId = request.EntityId
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
            }
        }

        return new DetachEntityFromDataSourceResponse();
    }
}