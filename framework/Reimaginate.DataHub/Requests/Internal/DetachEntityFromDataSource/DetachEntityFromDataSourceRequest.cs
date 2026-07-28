using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DetachEntityFromDataSource;

public class DetachEntityFromDataSourceRequest : IRequest<DetachEntityFromDataSourceResponse>
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string DataSource { get; set; }
    public List<ChangeTrackingEntry> TrackingEntries { get; set; } = null;
    public bool SkipSave { get; set; }

}