using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetAlternateKeysResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<DataHubEntityAlternateKey> Results { get; set; }

    public int ResultCount { get; set; }

    public string ContinuationToken { get; set; }

    public bool MoreResultsAvailable { get; set; }
}