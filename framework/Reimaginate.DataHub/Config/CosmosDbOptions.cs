namespace Reimaginate.DataHub.Config;

public enum CosmosDbAuthenticationMode
{
    ConnectionString,
    ApplicationRegistration,
    ManagedIdentity
}

public class CosmosDbOptions
{
    public CosmosDbAuthenticationMode AuthenticationMode { get; set; } = CosmosDbAuthenticationMode.ConnectionString;
    public string ConnString { get; set; }
    public string AccountEndpoint { get; set; }
    public string TenantId { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string ManagedIdentityClientId { get; set; }
    public string Database { get; set; }
    public bool AutoCreateContainers { get; set; }
    public string DataContainer { get; set; } = "ResultingEntity";
    public string TrackingDataContainer { get; set; } = "TrackingData";
    public string SyncMarkersContainer { get; set; } = "Control";
    public string SyncFailuresContainer { get; set; } = "Control";
    public string ResolutionPromisesContainer { get; set; } = "Control";
    public string ConfigsContainer { get; set; } = "Configs";
    public string ManagementContainer { get; set; } = "Management";
    public bool? UseGateway { get; set; }
    public int MaxRetryAttemptsOnRateLimitedRequests { get; set; } = 500;
    public int MaxRetryWaitSecondsOnRateLimitedRequests { get; set; } = 300;
}
