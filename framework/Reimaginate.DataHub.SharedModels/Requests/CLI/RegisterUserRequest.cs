using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RegisterUserRequest : DataHubCLIRequest<RegisterUserResponse>
{
    public RegisterUserRequest()
    {
        RequestType = nameof(RegisterUserRequest);
    }
    public string TenantId { get; set; }
    public string EntraObjectId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string UPN { get; set; }
    public List<string> Roles { get; set; }
}
