using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;

namespace Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;

public class ProcessPatchEntityResponse
{
    public string RequestId { get; set; }
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<PatchFailure> PatchFailures { get; set; }
    public bool IsTrackedEntity { get; set; }
    public JObject ChangeSet { get; set; }
    public JObject UpdatedEntity { get; set; }
    public bool IsDataHubEntity { get; set; }
    public bool Notify { get; set; }
}