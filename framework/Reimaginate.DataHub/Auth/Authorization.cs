using System;
using System.Collections.Generic;
using System.Linq;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Auth;

public static class Authorization
{
    private static readonly Dictionary<string, List<string>> Roles = new()
    {
        { DataHubRoles.Admin, ["*"] },
        { DataHubRoles.Reader, [
            DataHubPermissions.QueryEntities,
            DataHubPermissions.QuerySyncFailures,
            DataHubPermissions.QuerySyncMarkers,
            DataHubPermissions.QueryTrackingData,
            ]
        }
    };

    public static IReadOnlyDictionary<string, List<string>> BuiltInRoles => Roles;

    public static bool HasPermissions(User user, string permission)
    {
        if (user == null)
        {
            return false;
        }

        if (user.EffectivePermissions?.Any() == true)
        {
            return user.EffectivePermissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ||
                   user.EffectivePermissions.Contains("*", StringComparer.OrdinalIgnoreCase);
        }

        if (user.Roles == null || !user.Roles.Any()) return false;

        foreach (var userRole in user.Roles)
        {
            if (!Roles.TryGetValue(NormalizeRoleName(userRole), out var role)) throw new Exception("INVALID_ROLE");

            if (role.Contains(permission, StringComparer.OrdinalIgnoreCase) || role.Contains("*"))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsBuiltInRole(string roleName)
    {
        var normalizedRoleName = NormalizeRoleName(roleName);
        return !string.IsNullOrWhiteSpace(normalizedRoleName) && Roles.ContainsKey(normalizedRoleName);
    }

    public static string NormalizeRoleName(string roleName)
    {
        return roleName?.Trim().ToLowerInvariant();
    }
}
