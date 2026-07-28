using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetRolesRequest : DataHubCLIRequest<GetRolesResponse>
{
    public GetRolesRequest()
    {
        RequestType = nameof(GetRolesRequest);
    }

    public string Where { get; set; }
}

public class GetRolesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<DataHubRoleDTO> Results { get; set; } = new();
}
