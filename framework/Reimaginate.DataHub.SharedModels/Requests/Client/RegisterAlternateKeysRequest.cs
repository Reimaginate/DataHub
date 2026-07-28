using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterAlternateKeysRequest : DataHubClientRequest<RegisterAlternateKeysResponse>
{
    public RegisterAlternateKeysRequest()
    {
        RequestType = nameof(RegisterAlternateKeysRequest);
    }

    public List<RegisterAlternateKeyRequest> Requests { get; set; }
    public bool Silent { get; set; }

}