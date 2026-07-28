using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core.Models.Events;

public class CustomEvent : Event
{
    public string Type { get; set; }
    public JToken Data { get; set; }
}