using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapRoleToRoleDto : ITypeMapper<DataHubRole, DataHubRoleDTO>
{
    public Task<DataHubRoleDTO> MapAsync(DataHubRole from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        return Task.FromResult(new DataHubRoleDTO
        {
            Id = from.id,
            TenantId = from.TenantId,
            Name = from.Name,
            Description = from.Description,
            BuiltIn = false,
            Permissions = from.Permissions
        });
    }
}
