using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.GetUser;

public class GetUserResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public User Result { get; set; }
}