using System;
using Azure.Core;
using Azure.Identity;

namespace Reimaginate.DataHub.Client.Config;

public static class DataHubClientCredentialFactory
{
    public static TokenCredential CreateCredential(DataHubClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        return options.AuthenticationMode switch
        {
            DataHubClientAuthenticationMode.ApplicationRegistration => new ClientSecretCredential(
                options.TenantId,
                options.ClientId,
                options.ClientSecret),
            DataHubClientAuthenticationMode.ManagedIdentity => CreateManagedIdentityCredential(options),
            DataHubClientAuthenticationMode.SharedKey => throw new InvalidOperationException($"DataHub client authentication mode '{options.AuthenticationMode}' does not use Azure identity credentials."),
            _ => throw new InvalidOperationException($"Unsupported DataHub client authentication mode '{options.AuthenticationMode}'.")
        };
    }

    public static void Validate(DataHubClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        EnsureConfigured(options.DataHubClientUrl, nameof(DataHubClientOptions.DataHubClientUrl), options.AuthenticationMode);

        switch (options.AuthenticationMode)
        {
            case DataHubClientAuthenticationMode.SharedKey:
                EnsureConfigured(options.Key, nameof(DataHubClientOptions.Key), options.AuthenticationMode);
                break;
            case DataHubClientAuthenticationMode.ApplicationRegistration:
                EnsureConfigured(options.AzureAdScope, nameof(DataHubClientOptions.AzureAdScope), options.AuthenticationMode);
                EnsureConfigured(options.TenantId, nameof(DataHubClientOptions.TenantId), options.AuthenticationMode);
                EnsureConfigured(options.ClientId, nameof(DataHubClientOptions.ClientId), options.AuthenticationMode);
                EnsureConfigured(options.ClientSecret, nameof(DataHubClientOptions.ClientSecret), options.AuthenticationMode);
                break;
            case DataHubClientAuthenticationMode.ManagedIdentity:
                EnsureConfigured(options.AzureAdScope, nameof(DataHubClientOptions.AzureAdScope), options.AuthenticationMode);
                break;
            default:
                throw new InvalidOperationException($"Unsupported DataHub client authentication mode '{options.AuthenticationMode}'.");
        }
    }

    private static ManagedIdentityCredential CreateManagedIdentityCredential(DataHubClientOptions options)
    {
        return string.IsNullOrWhiteSpace(options.ManagedIdentityClientId)
            ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
            : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(options.ManagedIdentityClientId));
    }

    private static void EnsureConfigured(string value, string optionName, DataHubClientAuthenticationMode authenticationMode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"DataHubClientOptions.{optionName} must be configured when AuthenticationMode is {authenticationMode}.");
        }
    }
}
