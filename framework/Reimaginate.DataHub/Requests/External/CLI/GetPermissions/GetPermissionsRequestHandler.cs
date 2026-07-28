using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetPermissions;

public class GetPermissionsRequestHandler : IHandler<GetPermissionsRequest, GetPermissionsResponse>
{
    public Task<GetPermissionsResponse> HandleAsync(GetPermissionsRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new GetPermissionsResponse
        {
            Success = true,
            Results = DataHubPermissionCatalog.GetPermissions()
                .Select(permission => new DataHubPermissionDTO { Name = permission })
                .ToList()
        });
    }
}
