using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;

public class CheckPreMergeRulesRequest : IRequest<CheckPreMergeRulesResponse>
{
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string DataHubEntityType { get; set; }
    public EntityConfig EntityConfig { get; set; }
    public JObject IncomingEntity { get; set; }
    public JObject ExistingEntity { get; set; }
    public ChangeTrackingEntry ChangeSet { get; set; }
    public string Context { get; set; } = "*";
   
}