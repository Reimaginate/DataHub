using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;

public class MaterializeDataHubEntityRequest : IRequest<MaterializeDataHubEntityResponse>
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public List<ChangeTrackingEntry> TrackingEntries { get; set; }
    public bool SkipSave { get; set; }
}