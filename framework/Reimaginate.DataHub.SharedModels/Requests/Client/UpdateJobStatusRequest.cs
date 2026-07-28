using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateJobStatusRequest : DataHubClientRequest<UpdateJobStatusResponse>
{
    public UpdateJobStatusRequest()
    {
        RequestType = nameof(UpdateJobStatusRequest);
    }
    public string JobId { get; set; }
    public string Status { get; set; }
    public JToken Response { get; set; }
}