using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RegisterAlternateKeysResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<RegisterAlternateKeyResponse> Responses { get; set; }
}