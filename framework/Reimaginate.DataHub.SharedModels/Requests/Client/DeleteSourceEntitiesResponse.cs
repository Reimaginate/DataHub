using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DeleteSourceEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<DeleteSourceEntityFailure> Failures { get; set; } = new();
}