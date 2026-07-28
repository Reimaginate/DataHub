using System;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace Reimaginate.DataHub.Config;

internal static class CosmosClientFactory
{
    public static CosmosClient Create(CosmosDbOptions options, CosmosClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        return options.AuthenticationMode switch
        {
            CosmosDbAuthenticationMode.ConnectionString => CreateWithConnectionString(options, clientOptions),
            CosmosDbAuthenticationMode.ApplicationRegistration => CreateWithApplicationRegistration(options, clientOptions),
            CosmosDbAuthenticationMode.ManagedIdentity => CreateWithManagedIdentity(options, clientOptions),
            _ => throw new InvalidOperationException($"Unsupported Cosmos DB authentication mode '{options.AuthenticationMode}'.")
        };
    }

    internal static TokenCredential CreateCredential(CosmosDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.AuthenticationMode switch
        {
            CosmosDbAuthenticationMode.ApplicationRegistration => CreateApplicationRegistrationCredential(options),
            CosmosDbAuthenticationMode.ManagedIdentity => CreateManagedIdentityCredential(options),
            _ => throw new InvalidOperationException($"Cosmos DB authentication mode '{options.AuthenticationMode}' does not use Azure identity credentials.")
        };
    }

    private static CosmosClient CreateWithConnectionString(CosmosDbOptions options, CosmosClientOptions clientOptions)
    {
        EnsureConfigured(options.ConnString, nameof(CosmosDbOptions.ConnString), options.AuthenticationMode);

        return new CosmosClient(options.ConnString, clientOptions);
    }

    private static CosmosClient CreateWithApplicationRegistration(CosmosDbOptions options, CosmosClientOptions clientOptions)
    {
        EnsureConfigured(options.AccountEndpoint, nameof(CosmosDbOptions.AccountEndpoint), options.AuthenticationMode);

        return new CosmosClient(options.AccountEndpoint, CreateApplicationRegistrationCredential(options), clientOptions);
    }

    private static CosmosClient CreateWithManagedIdentity(CosmosDbOptions options, CosmosClientOptions clientOptions)
    {
        EnsureConfigured(options.AccountEndpoint, nameof(CosmosDbOptions.AccountEndpoint), options.AuthenticationMode);

        return new CosmosClient(options.AccountEndpoint, CreateManagedIdentityCredential(options), clientOptions);
    }

    private static ClientSecretCredential CreateApplicationRegistrationCredential(CosmosDbOptions options)
    {
        EnsureConfigured(options.TenantId, nameof(CosmosDbOptions.TenantId), options.AuthenticationMode);
        EnsureConfigured(options.ClientId, nameof(CosmosDbOptions.ClientId), options.AuthenticationMode);
        EnsureConfigured(options.ClientSecret, nameof(CosmosDbOptions.ClientSecret), options.AuthenticationMode);

        return new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);
    }

    private static ManagedIdentityCredential CreateManagedIdentityCredential(CosmosDbOptions options)
    {
        return string.IsNullOrWhiteSpace(options.ManagedIdentityClientId)
            ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
            : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(options.ManagedIdentityClientId));
    }

    private static void EnsureConfigured(string value, string optionName, CosmosDbAuthenticationMode authenticationMode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"CosmosDbOptions.{optionName} must be configured when AuthenticationMode is {authenticationMode}.");
        }
    }
}
