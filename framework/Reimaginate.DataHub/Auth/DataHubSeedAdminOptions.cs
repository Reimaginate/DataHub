using System.Collections.Generic;

namespace Reimaginate.DataHub.Auth;

public class DataHubSeedAdminOptions
{
    public List<DataHubSeedAdminUser> Users { get; set; } = new();
}

public class DataHubSeedAdminUser
{
    public string TenantId { get; set; }
    public string EntraObjectId { get; set; }
    public string UPN { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public List<string> Roles { get; set; } = [DataHubRoles.Admin];
}
