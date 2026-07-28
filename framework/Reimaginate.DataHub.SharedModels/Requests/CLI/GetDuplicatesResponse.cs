using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetDuplicatesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<Duplicate> Results { get; set; }
    public int ResultCount { get; set; }
    public string ContinuationToken { get; set; }
    public bool MoreResultsAvailable { get; set; }
}