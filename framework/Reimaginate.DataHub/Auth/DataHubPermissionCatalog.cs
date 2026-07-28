using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Reimaginate.DataHub.Auth;

public static class DataHubPermissionCatalog
{
    private static readonly Lazy<IReadOnlyList<string>> PermissionNames = new(() =>
        typeof(DataHubPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string))
            .Select(field => (string)field.GetValue(null))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public static IReadOnlyList<string> GetPermissions()
    {
        return PermissionNames.Value;
    }

    public static bool IsKnownPermission(string permission)
    {
        return PermissionNames.Value.Contains(permission?.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> NormalizePermissions(IEnumerable<string> permissions)
    {
        return permissions?
            .Select(permission => permission?.Trim().ToLowerInvariant())
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
    }

    public static List<string> GetUnknownPermissions(IEnumerable<string> permissions)
    {
        return NormalizePermissions(permissions)
            .Where(permission => !IsKnownPermission(permission))
            .ToList();
    }
}
