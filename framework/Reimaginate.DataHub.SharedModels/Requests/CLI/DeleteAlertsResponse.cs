using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteAlertsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<DeleteLogFailure> DeleteFailures { get; set; }
}