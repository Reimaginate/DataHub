using System;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateEntityRequest : DataHubClientRequest<UpdateEntityResponse>
{
    public UpdateEntityRequest()
    {
        RequestType = nameof(UpdateEntityRequest);
    }

    public string UpdateType { get; set; }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public JObject Data { get; set; }
    public bool CreateIfMissing { get; set; }
    public bool ReturnResultingEntity { get; set; } = false;
}