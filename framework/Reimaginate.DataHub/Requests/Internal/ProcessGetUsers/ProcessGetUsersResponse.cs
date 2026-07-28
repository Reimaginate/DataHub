using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetUsers;

public class ProcessGetUsersResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public PagedResults<User> PagedResults { get; set; }

}