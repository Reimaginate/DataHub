namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterAlternateKeyRequest : DataHubClientRequest<RegisterAlternateKeyResponse>
{
    public RegisterAlternateKeyRequest()
    {
        RequestType = nameof(RegisterAlternateKeyRequest);
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