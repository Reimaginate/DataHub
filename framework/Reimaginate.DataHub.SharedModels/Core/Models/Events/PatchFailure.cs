using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core.Models.Events;

public class PatchFailure : Event
{
    public string EventSource { get; set; }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public JToken Patch { get; set; }
    public string FailureReason { get; set; }
}