using System.Collections.Generic;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKey;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKeys;

public class ProcessRegisterAlternateKeysResponse
{
    public List<ProcessRegisterAlternateKeyResponse> Responses { get; set; }
}