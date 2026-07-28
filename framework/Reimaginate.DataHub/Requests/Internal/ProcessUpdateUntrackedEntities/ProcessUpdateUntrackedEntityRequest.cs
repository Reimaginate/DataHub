using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateUntrackedEntities;

public class ProcessUpdateUntrackedEntityRequest 
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public JObject Data { get; set; }
    public bool? CreateIfMissing { get; set; }
    public bool DispatchNotifications { get; set; }
    public bool Silent { get; set; }
}