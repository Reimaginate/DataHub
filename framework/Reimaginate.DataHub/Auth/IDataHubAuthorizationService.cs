using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Auth;

public interface IDataHubAuthorizationService
{
    Task<User> ResolveEffectivePermissionsAsync(User user, CancellationToken cancellationToken);
    Task<bool> RoleExistsAsync(string tenantId, string roleName, CancellationToken cancellationToken);
    Task<bool> ValidateRoleReferencesAsync(string tenantId, IEnumerable<string> roleNames, CancellationToken cancellationToken);
}
