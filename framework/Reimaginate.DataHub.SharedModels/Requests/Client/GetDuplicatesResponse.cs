using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDuplicatesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<DuplicateDTO> Results { get; set; }
    public int ResultCount { get; set; }
    public string ContinuationToken { get; set; }
    public bool MoreResultsAvailable { get; set; }
}