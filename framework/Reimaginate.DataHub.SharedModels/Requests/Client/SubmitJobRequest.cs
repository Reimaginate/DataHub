using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class SubmitJobRequest : DataHubClientRequest<SubmitJobResponse>
{
    public SubmitJobRequest()
    {
        RequestType = nameof(SubmitJobRequest);
    }
    public string Type { get; set; }
    public string Name { get; set; }
    public string Target { get; set; }
    public JToken Request { get; set; }
    public JToken Response { get; set; }
    public string Status { get; set; }
    public bool DisableNotifications { get; set; }
}