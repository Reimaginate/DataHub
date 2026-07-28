using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapUserToUserDto : ITypeMapper<User, UserDTO>
{
    public Task<UserDTO> MapAsync(User from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        var ret = new UserDTO()
        {
            Id = from.id,
            TenantId = from.TenantId,
            EntraObjectId = from.EntraObjectId,
            UPN = from.UPN,
            Name = from.Name,
            Email = from.Email,
            Disabled = from.Disabled,
            Roles = from.Roles,
            EffectivePermissions = from.EffectivePermissions
        };

        return Task.FromResult(ret);
    }
}
