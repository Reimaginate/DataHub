using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reimaginate.DataHub.SharedModels.Core;

public class User : CosmosDocument
{
    public User()
    {
        _dt = nameof(User);
    }

    public string Name { get; set; }
    public string Email { get; set; }
    public string TenantId { get; set; }
    public string EntraObjectId { get; set; }
    public string UPN { get; set; }
    public List<string> Roles { get; set; } = new();
    [JsonIgnore]
    public List<string> EffectivePermissions { get; set; } = new();
    public bool Disabled { get; set; }
}
