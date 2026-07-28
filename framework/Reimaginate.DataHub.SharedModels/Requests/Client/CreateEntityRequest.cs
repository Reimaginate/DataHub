using System;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class CreateEntityRequest : DataHubClientRequest<CreateEntityResponse>
{
    public CreateEntityRequest()
    {
        RequestType = nameof(CreateEntityRequest);
    }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public JObject Data { get; set; }

    public bool ReturnResultingEntity { get; set; } = false;
}