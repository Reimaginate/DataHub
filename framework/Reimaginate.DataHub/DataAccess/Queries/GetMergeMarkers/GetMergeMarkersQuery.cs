using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetMergeMarkers;

public class GetMergeMarkersQuery : IRequest<List<MergeMarker>>
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
}