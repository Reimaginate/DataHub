using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core.Models.Events;

public class Alert : Event
{
    public string Severity { get; set; }
    public string Subject { get; set; }
    public string Description { get; set; }
    public JToken Data { get; set; }
}