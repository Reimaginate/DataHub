using Newtonsoft.Json;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Models;

public class DataHubConnection
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("url")]
    public string DataHubUrl { get; set; } = string.Empty;

    [JsonProperty("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonProperty("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonIgnore]
    public string AccessToken { get; set; } = string.Empty;
}
