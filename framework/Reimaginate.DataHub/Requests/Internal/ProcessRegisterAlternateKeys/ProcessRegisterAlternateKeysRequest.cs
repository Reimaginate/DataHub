using System.Collections.Generic;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKey;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKeys;

public class ProcessRegisterAlternateKeysRequest : DataHubClientRequest<ProcessRegisterAlternateKeysResponse>
{
    public ProcessRegisterAlternateKeysRequest()
    {
        RequestType = nameof(ProcessRegisterAlternateKeysRequest);
    }

    public List<ProcessRegisterAlternateKeyRequest> Requests { get; set; }

    public bool Silent { get; set; }
    
}