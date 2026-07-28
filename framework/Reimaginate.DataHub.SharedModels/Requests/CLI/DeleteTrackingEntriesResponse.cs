using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteTrackingEntriesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<DeleteTrackingDataEntryFailure> Failures { get; set; }
}