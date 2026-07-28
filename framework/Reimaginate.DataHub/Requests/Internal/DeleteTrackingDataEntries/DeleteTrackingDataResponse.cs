using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;

namespace Reimaginate.DataHub.Requests.Internal.DeleteTrackingDataEntries;

public class DeleteTrackingDataResponse
{
    public bool Success { get; set; }
    public List<DeleteTrackingDataEntryFailure> Failures { get; set; } = new();
}