using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DisableUserRequest : DataHubCLIRequest<DisableUserResponse>
{
    public DisableUserRequest()
    {
        RequestType = nameof(DisableUserRequest);
    }
    public string Id { get; set; }
}