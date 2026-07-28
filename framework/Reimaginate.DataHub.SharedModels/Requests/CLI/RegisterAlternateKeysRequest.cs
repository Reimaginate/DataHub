using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RegisterAlternateKeysRequest : DataHubCLIRequest<RegisterAlternateKeysResponse>
{
    public RegisterAlternateKeysRequest()
    {
        RequestType = nameof(RegisterAlternateKeysRequest);
    }

    public List<RegisterAlternateKeyRequest> Requests { get; set; }
    public bool Silent { get; set; }
}