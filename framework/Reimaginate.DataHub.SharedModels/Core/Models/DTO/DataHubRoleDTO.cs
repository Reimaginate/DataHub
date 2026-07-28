using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Core.Models.DTO;

public class DataHubRoleDTO
{
    public string Id { get; set; }
    public string TenantId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool BuiltIn { get; set; }
    public List<string> Permissions { get; set; } = new();
}
