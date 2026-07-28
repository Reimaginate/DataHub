using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Models;

public class Blob
{
    public string Name { get; set; } = string.Empty;
    public JArray Payload { get; set; } = [];
}
