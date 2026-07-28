using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;

public class MaterializeDataHubEntityRequestHandler(IMediator mediator) : IHandler<MaterializeDataHubEntityRequest, MaterializeDataHubEntityResponse>
{
    public async Task<MaterializeDataHubEntityResponse> HandleAsync(MaterializeDataHubEntityRequest request, CancellationToken cancellationToken)
    {
        var trackedEntity = (await mediator.TrySend( new GetTrackedEntityRequest()
        {
            TrackingEntries = request.TrackingEntries,
            EntityType = request.EntityType,
            DataSource = DataSources.DataHub,
            EntityId = request.EntityId
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        trackedEntity = trackedEntity.RemoveNullValues();

        if (request.SkipSave) return new MaterializeDataHubEntityResponse()
        {
            ResultingEntity = trackedEntity
        };

        var upsertResult = (await mediator.TrySend( new UpsertDataHubEntitiesCommand()
        {
            Entities = new List<JObject>() { trackedEntity }
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new MaterializeDataHubEntityResponse()
        {
            ResultingEntity = upsertResult.Successes.First()
        };
    }
}