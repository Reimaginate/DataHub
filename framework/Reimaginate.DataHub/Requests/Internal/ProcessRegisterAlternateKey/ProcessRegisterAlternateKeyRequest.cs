namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKey;

public class ProcessRegisterAlternateKeyRequest : DataHubClientRequest<ProcessRegisterAlternateKeyResponse>
{
    public ProcessRegisterAlternateKeyRequest()
    {
        RequestType = nameof(ProcessRegisterAlternateKeyRequest);
    }

    public string EntityType { get; set; }
    public bool Untracked { get; set; }
    public string Key { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityId { get; set; }
    public bool Replace { get; set; }
    public bool ReplaceSameDataSource { get; set; }

    public bool Silent { get; set; }
}