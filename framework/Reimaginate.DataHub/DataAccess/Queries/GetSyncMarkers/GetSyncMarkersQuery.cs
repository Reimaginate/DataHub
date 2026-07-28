using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetSyncMarkers;

public class GetSyncMarkersQuery : IRequest<List<SyncMarker>>
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
}