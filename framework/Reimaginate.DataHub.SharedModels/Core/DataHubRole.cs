using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Core;

public class DataHubRole : CosmosDocument
{
    public DataHubRole()
    {
        _dt = nameof(DataHubRole);
    }

    public string TenantId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}
