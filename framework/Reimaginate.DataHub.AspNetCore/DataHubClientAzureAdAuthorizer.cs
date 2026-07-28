using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Reimaginate.DataHub.AspNetCore;

public static class DataHubClientAzureAdAuthorizer
{
    private static readonly string[] ClientIdClaimTypes = { "azp", "appid" };
    private static readonly string[] ObjectIdClaimTypes =
    {
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier"
    };

    public static async Task<bool> AuthorizeAsync(
        HttpRequest request,
        string authenticationScheme,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var authenticateResult = await request.HttpContext.AuthenticateAsync(authenticationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            return false;
        }

        var options = request.HttpContext.RequestServices
            .GetRequiredService<IOptions<DataHubClientAzureAdOptions>>()
            .Value;

        return IsAllowedApplicationToken(authenticateResult.Principal, options);
    }

    public static bool IsAllowedApplicationToken(ClaimsPrincipal principal, DataHubClientAzureAdOptions options)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(options);

        if (principal.HasClaim(claim => string.Equals(claim.Type, "scp", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return HasAllowedClaim(principal, ClientIdClaimTypes, options.AllowedClientIds)
            || HasAllowedClaim(principal, ObjectIdClaimTypes, options.AllowedObjectIds);
    }

    private static bool HasAllowedClaim(ClaimsPrincipal principal, string[] claimTypes, string[] allowedValues)
    {
        if (allowedValues == null || allowedValues.Length == 0)
        {
            return false;
        }

        return principal.Claims
            .Where(claim => claimTypes.Any(type => string.Equals(type, claim.Type, StringComparison.OrdinalIgnoreCase)))
            .Select(claim => claim.Value)
            .Any(value => allowedValues.Any(allowedValue => string.Equals(allowedValue, value, StringComparison.OrdinalIgnoreCase)));
    }
}
