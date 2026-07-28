using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Rules;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessNewEntity;

public class ProcessNewEntityRequest : IRequest<ProcessNewEntityResponse>
{
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityType { get; set; }

    public EntityConfig EntityConfig { get; set; }

    public DuplicatePreventionRule DuplicatePreventionRule { get; set; }
    public JArray PotentialDuplicates { get; set; }

    public JObject SourceEntity { get; set; }
    public bool DoNotTrack { get; set; } = false;
}