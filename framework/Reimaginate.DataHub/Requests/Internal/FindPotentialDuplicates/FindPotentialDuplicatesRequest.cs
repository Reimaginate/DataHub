using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Rules;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.FindPotentialDuplicates;

public class FindPotentialDuplicatesRequest : IRequest<FindPotentialDuplicatesResponse>
{
    public string DataHubEntityType { get; set; }
    public DuplicatePreventionRule DuplicatePreventionRule { get; set; }
    public List<JObject> EntitiesToMatch { get; set; }
}