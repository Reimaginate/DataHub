using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDuplicateResponse {
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public Duplicate Result { get; set; }
}