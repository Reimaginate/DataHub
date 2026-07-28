using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.AspNetCore;

public sealed class DataHubClientEndpointOptions
{
    public Func<HttpRequest, CancellationToken, Task<bool>> AuthorizeAsync { get; set; }

    public DataHubClientEndpointOptions UseAzureAdBearerAuthorization(
        string authenticationScheme = DataHubClientEndpointAuthenticationDefaults.AzureAdBearerScheme)
    {
        AuthorizeAsync = (request, cancellationToken) =>
            DataHubClientAzureAdAuthorizer.AuthorizeAsync(request, authenticationScheme, cancellationToken);
        return this;
    }
}

public sealed class DataHubCliEndpointOptions
{
    public Func<HttpRequest, CancellationToken, Task<User>> AuthenticateAsync { get; set; }

    public DataHubCliEndpointOptions UseAzureAdCliAuthentication(
        string authenticationScheme = DataHubCliEndpointAuthenticationDefaults.AzureAdBearerScheme)
    {
        AuthenticateAsync = (request, cancellationToken) =>
            DataHubCliAzureAdAuthenticator.AuthenticateAsync(request, authenticationScheme, cancellationToken);
        return this;
    }
}

public sealed class DataHubClientAzureAdOptions
{
    public string TenantId { get; set; }
    public string Audience { get; set; }
    public string[] AllowedClientIds { get; set; } = Array.Empty<string>();
    public string[] AllowedObjectIds { get; set; } = Array.Empty<string>();
}

public sealed class DataHubCliAzureAdOptions
{
    public string TenantId { get; set; }
    public string Audience { get; set; }
    public string RequiredScope { get; set; } = "datahub_cli";
}
