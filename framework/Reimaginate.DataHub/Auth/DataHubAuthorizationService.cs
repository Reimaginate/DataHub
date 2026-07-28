using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Auth;

public class DataHubAuthorizationService(IMediator mediator) : IDataHubAuthorizationService
{
    public async Task<User> ResolveEffectivePermissionsAsync(User user, CancellationToken cancellationToken)
    {
        if (user == null)
        {
            return null;
        }

        var effectivePermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in NormalizeRoles(user.Roles))
        {
            if (Authorization.BuiltInRoles.TryGetValue(roleName, out var builtInPermissions))
            {
                foreach (var permission in builtInPermissions)
                {
                    effectivePermissions.Add(permission);
                }

                continue;
            }

            var role = await GetTenantRoleAsync(user.TenantId, roleName, cancellationToken);
            if (role == null)
            {
                return null;
            }

            foreach (var permission in role.Permissions ?? new List<string>())
            {
                effectivePermissions.Add(permission);
            }
        }

        user.Roles = NormalizeRoles(user.Roles);
        user.EffectivePermissions = effectivePermissions
            .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return user;
    }

    public async Task<bool> RoleExistsAsync(string tenantId, string roleName, CancellationToken cancellationToken)
    {
        var normalizedRoleName = Authorization.NormalizeRoleName(roleName);
        if (string.IsNullOrWhiteSpace(normalizedRoleName))
        {
            return false;
        }

        if (Authorization.IsBuiltInRole(normalizedRoleName))
        {
            return true;
        }

        return await GetTenantRoleAsync(tenantId, normalizedRoleName, cancellationToken) != null;
    }

    public async Task<bool> ValidateRoleReferencesAsync(string tenantId, IEnumerable<string> roleNames, CancellationToken cancellationToken)
    {
        foreach (var roleName in NormalizeRoles(roleNames))
        {
            if (!await RoleExistsAsync(tenantId, roleName, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<DataHubRole> GetTenantRoleAsync(string tenantId, string normalizedRoleName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(normalizedRoleName))
        {
            return null;
        }

        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<DataHubRole>
        {
            WhereClause = "x.TenantId = @tenantId and x.Name = @roleName",
            PageSize = 2,
            Parameters =
            [
                new QueryParameter("tenantId", tenantId),
                new QueryParameter("roleName", normalizedRoleName)
            ]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return response.Results.Count == 1 ? response.Results.First() : null;
    }

    private static List<string> NormalizeRoles(IEnumerable<string> roles)
    {
        return roles?
            .Select(Authorization.NormalizeRoleName)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
    }
}
