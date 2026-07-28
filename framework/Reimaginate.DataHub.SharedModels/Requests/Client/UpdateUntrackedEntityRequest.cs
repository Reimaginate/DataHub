using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateUntrackedEntityRequest : DataHubClientRequest<UpdateUntrackedEntityResponse>
{
    public UpdateUntrackedEntityRequest()
    {
        RequestType = nameof(UpdateUntrackedEntityRequest);
    }

    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public JObject Data { get; set; }
    public bool? CreateIfMissing { get; set; }
    public bool DispatchNotifications { get; set; }
    public bool Silent { get; set; }
}