using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.Requests.Internal.GetUser;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.AspNetCore;

public static class DataHubCliAzureAdAuthenticator
{
    private static readonly string[] TenantIdClaimTypes =
    {
        "tid",
        "http://schemas.microsoft.com/identity/claims/tenantid"
    };

    private static readonly string[] ObjectIdClaimTypes =
    {
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier"
    };

    private static readonly string[] UserPrincipalNameClaimTypes =
    {
        "preferred_username",
        "upn",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn",
        "email"
    };

    public static async Task<User> AuthenticateAsync(
        HttpRequest request,
        string authenticationScheme,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var authenticateResult = await request.HttpContext.AuthenticateAsync(authenticationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            return null;
        }

        var options = request.HttpContext.RequestServices
            .GetRequiredService<IOptions<DataHubCliAzureAdOptions>>()
            .Value;

        var principal = authenticateResult.Principal;
        if (!HasRequiredDelegatedScope(principal, options.RequiredScope))
        {
            return null;
        }

        var tenantId = FirstClaimValue(principal, TenantIdClaimTypes);
        var objectId = FirstClaimValue(principal, ObjectIdClaimTypes);
        var upn = FirstClaimValue(principal, UserPrincipalNameClaimTypes);

        if (string.IsNullOrWhiteSpace(tenantId)
            || (string.IsNullOrWhiteSpace(objectId) && string.IsNullOrWhiteSpace(upn)))
        {
            return null;
        }

        var mediator = request.HttpContext.RequestServices.GetRequiredService<IMediator>();
        var getUserResponse = (await mediator.TrySend<GetUserResponse>(new GetUserRequest
        {
            TenantId = tenantId,
            EntraObjectId = objectId,
            UserId = upn
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (getUserResponse is not { Success: true, Result: { } user } || user.Disabled)
        {
            return null;
        }

        var authorizationService = request.HttpContext.RequestServices.GetService<IDataHubAuthorizationService>();
        if (authorizationService == null)
        {
            return user;
        }

        return await authorizationService.ResolveEffectivePermissionsAsync(user, cancellationToken);
    }

    public static bool HasRequiredDelegatedScope(ClaimsPrincipal principal, string requiredScope)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (string.IsNullOrWhiteSpace(requiredScope))
        {
            return false;
        }

        var scopes = principal.Claims
            .Where(claim => string.Equals(claim.Type, "scp", StringComparison.OrdinalIgnoreCase))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return scopes.Any(scope => string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstClaimValue(ClaimsPrincipal principal, string[] claimTypes)
    {
        return principal.Claims
            .Where(claim => claimTypes.Any(type => string.Equals(type, claim.Type, StringComparison.OrdinalIgnoreCase)))
            .Select(claim => claim.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
