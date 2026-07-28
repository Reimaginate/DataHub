using System;
using Microsoft.Extensions.Configuration;

namespace Reimaginate.DataHub.Client.Config;

public class AddDataHubClientOptions
{
    internal IConfiguration Config { get; set; }
    internal DataHubClientOptions DataHubClientOptions { get; set; } = new();

    public AddDataHubClientOptions WithConnectionString(string dataHubUrl)
    {
        DataHubClientOptions.DataHubClientUrl = dataHubUrl;
        return this;
    }

    public AddDataHubClientOptions WithKey(string key)
    {
        DataHubClientOptions.Key = key;
        return this;
    }

    public AddDataHubClientOptions WithSharedKey(string dataHubUrl, string key)
    {
        DataHubClientOptions.AuthenticationMode = DataHubClientAuthenticationMode.SharedKey;
        DataHubClientOptions.DataHubClientUrl = dataHubUrl;
        DataHubClientOptions.Key = key;
        return this;
    }

    public AddDataHubClientOptions WithApplicationRegistration(
        string dataHubUrl,
        string azureAdScope,
        string tenantId,
        string clientId,
        string clientSecret)
    {
        DataHubClientOptions.AuthenticationMode = DataHubClientAuthenticationMode.ApplicationRegistration;
        DataHubClientOptions.DataHubClientUrl = dataHubUrl;
        DataHubClientOptions.AzureAdScope = azureAdScope;
        DataHubClientOptions.TenantId = tenantId;
        DataHubClientOptions.ClientId = clientId;
        DataHubClientOptions.ClientSecret = clientSecret;
        return this;
    }

    public AddDataHubClientOptions WithManagedIdentity(
        string dataHubUrl,
        string azureAdScope,
        string managedIdentityClientId = null)
    {
        DataHubClientOptions.AuthenticationMode = DataHubClientAuthenticationMode.ManagedIdentity;
        DataHubClientOptions.DataHubClientUrl = dataHubUrl;
        DataHubClientOptions.AzureAdScope = azureAdScope;
        DataHubClientOptions.ManagedIdentityClientId = managedIdentityClientId;
        return this;
    }
    
    public AddDataHubClientOptions WithAppSettingsConfig(IConfiguration config, string key = null)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        if (!string.IsNullOrEmpty(key))
        {
            Config = Config.GetSection(key);
        }

        Config.Bind(DataHubClientOptions);
        return this;
    }

}
