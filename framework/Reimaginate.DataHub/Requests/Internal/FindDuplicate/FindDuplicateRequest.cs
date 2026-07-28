using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Rules;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.FindDuplicate;

public class FindDuplicateRequest : IRequest<FindDuplicateResponse>
{
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public DuplicatePreventionRule DuplicatePreventionRule { get; set; }
    public JArray PotentialDuplicates { get; set; }
    public JObject EntityToMatch { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityType { get; set; }
}