using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessSplitDataHubEntityAlternateKey;

public class ProcessSplitDataHubEntityAlternateKeyRequest : IRequest<ProcessSplitDataHubEntityAlternateKeyResponse>
{
    public string CorrelationId { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public bool Silent { get; set; }
    public bool DryRun { get; set; }
}
