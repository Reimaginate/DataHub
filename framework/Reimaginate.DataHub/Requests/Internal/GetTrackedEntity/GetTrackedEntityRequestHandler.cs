using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesForEntity;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;

public class GetTrackedEntityRequestHandler(IMediator mediator) : IHandler<GetTrackedEntityRequest, JObject>
{
    public async Task<JObject> HandleAsync(GetTrackedEntityRequest request, CancellationToken cancellationToken)
    {
        var trackingEntries = request.TrackingEntries ?? (await mediator.TrySend(
            new GetTrackingEntriesForEntityQuery
            {
                DataSource = request.DataSource,
                EntityType = request.EntityType,
                EntityId = request.EntityId
            },
            cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!trackingEntries.Any()) return null;

        var timeScopedTrackingEntries = request.AtPointInTime == null ? trackingEntries : trackingEntries.Where(w => w.EntryType == ChangeTrackingEntryTypes.Init || w.Timestamp <= request.AtPointInTime).ToList();
        var trackedEntity = ChangeTrackingHelper.ReassembleEntity(timeScopedTrackingEntries);
        return trackedEntity;
    }
}