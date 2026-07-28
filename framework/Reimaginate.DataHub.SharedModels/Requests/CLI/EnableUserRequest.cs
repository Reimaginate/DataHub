using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class EnableUserRequest : DataHubCLIRequest<EnableUserResponse>
{
    public EnableUserRequest()
    {
        RequestType = nameof(EnableUserRequest);
    }
    public string Id { get; set; }
}