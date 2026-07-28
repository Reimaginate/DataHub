using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core;

public class Patch
{
    public string Operation { get; set; }
    public string Path { get; set; }
    public string Regex { get; set; }
    public JToken Value { get; set; }
    public bool DispatchNotifications { get; set; } = false;
}