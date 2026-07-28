using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Core;

public class AzureFunctionKeyResponse
{
    public List<AzureFunctionKey> Keys { get; set; }
}

public class AzureFunctionKey
{
    public string Name { get; set; }
    public string Value { get; set; }
}