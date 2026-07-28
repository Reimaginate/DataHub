using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class ReserveAutoNumbersResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public string SequenceName { get; set; }
    public List<ReservedAutoNumber> Numbers { get; set; } = new();
}
