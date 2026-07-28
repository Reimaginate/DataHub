using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.FindEntitiesByAlternateKey;

public class FindEntitiesByAlternateKeyRequest : IRequest<JArray>
{
    public string EntityType { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
}