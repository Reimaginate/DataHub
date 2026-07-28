namespace Reimaginate.DataHub.Client.Config
{
    public enum DataHubClientAuthenticationMode
    {
        SharedKey,
        ApplicationRegistration,
        ManagedIdentity
    }

    public class DataHubClientOptions
    {
        public DataHubClientAuthenticationMode AuthenticationMode { get; set; } = DataHubClientAuthenticationMode.SharedKey;
        public string DataHubClientUrl { get; set; }
        public string Key { get; set; }
        public string AzureAdScope { get; set; }
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string ManagedIdentityClientId { get; set; }
    }
}
