using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class SplitDataHubEntityAlternateKeyRequest : DataHubCLIRequest<SplitDataHubEntityAlternateKeyResponse>
{
    public SplitDataHubEntityAlternateKeyRequest()
    {
        RequestType = nameof(SplitDataHubEntityAlternateKeyRequest);
    }

    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public bool Silent { get; set; }
    public bool DryRun { get; set; }
}
