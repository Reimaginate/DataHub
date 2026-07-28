using System;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core;

public class RevertDataHubEntityResult
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public DateTimeOffset? RevertedTo { get; set; }
    public string TrackingEntryId { get; set; }
    public bool Changed { get; set; }
    public JObject ChangeSet { get; set; }
}
