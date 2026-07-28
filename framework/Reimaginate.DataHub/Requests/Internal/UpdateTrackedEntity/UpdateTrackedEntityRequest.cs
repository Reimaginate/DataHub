using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.UpdateTrackedEntity;

public class UpdateTrackedEntityRequest : IRequest<UpdateTrackedEntityResponse>
{
    public UpdateTrackedEntityRequest()
    {
    }
        
    public string DataSource { get; set; }
    public string SourceEntityId { get; set; }
    public string EntityType { get; set; }
    public JObject EntityData { get; set; }
    public List<ChangeTrackingEntry> TrackingEntries { get; set; }
    public bool SkipSave { get; set; }
}