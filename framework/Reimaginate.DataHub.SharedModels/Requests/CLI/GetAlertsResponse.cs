using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetAlertsResponse
{
    public string Select { get; set; }

    public List<AlertDTO> Results { get; set; }

    public int ResultCount { get; set; }

    public string ContinuationToken { get; set; }

    public bool MoreResultsAvailable { get; set; }
}