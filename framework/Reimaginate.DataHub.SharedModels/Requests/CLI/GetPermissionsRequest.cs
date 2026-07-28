using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetPermissionsRequest : DataHubCLIRequest<GetPermissionsResponse>
{
    public GetPermissionsRequest()
    {
        RequestType = nameof(GetPermissionsRequest);
    }
}

public class GetPermissionsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<DataHubPermissionDTO> Results { get; set; } = new();
}
