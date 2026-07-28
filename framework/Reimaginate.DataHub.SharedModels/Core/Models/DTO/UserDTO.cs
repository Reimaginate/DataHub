using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Core.Models.DTO;

public class UserDTO
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string UPN { get; set; }
    public string TenantId { get; set; }
    public string EntraObjectId { get; set; }
    public bool Disabled { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> EffectivePermissions { get; set; } = new();
}
