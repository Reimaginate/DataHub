using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessNewEntityIntoExistingEntity;

public class ProcessNewEntityIntoExistingEntityRequest : IRequest<ProcessNewEntityIntoExistingEntityResponse>
{
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }

    public JObject FromEntity { get; set; }
    public JObject ToEntity { get; set; }

    public EntityConfig EntityConfig { get; set; }
}