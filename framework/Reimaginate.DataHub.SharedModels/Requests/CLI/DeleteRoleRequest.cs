using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteRoleRequest : DataHubCLIRequest<DeleteRoleResponse>
{
    public DeleteRoleRequest()
    {
        RequestType = nameof(DeleteRoleRequest);
    }

    public string Name { get; set; }
}

public class DeleteRoleResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
}
