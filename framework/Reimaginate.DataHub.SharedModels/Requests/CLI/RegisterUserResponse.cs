using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RegisterUserResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }

    public User Result { get; set; }
}