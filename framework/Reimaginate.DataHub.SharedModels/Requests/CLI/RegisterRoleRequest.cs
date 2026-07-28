using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RegisterRoleRequest : DataHubCLIRequest<RegisterRoleResponse>
{
    public RegisterRoleRequest()
    {
        RequestType = nameof(RegisterRoleRequest);
    }

    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class RegisterRoleResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public DataHubRoleDTO Result { get; set; }
}
