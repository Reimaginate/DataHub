using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.RegisterRole;

public class RegisterRoleRequestHandler(IMediator mediator, IIdService idService, IMapper mapper) : IHandler<RegisterRoleRequest, RegisterRoleResponse>
{
    public async Task<RegisterRoleResponse> HandleAsync(RegisterRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = request.User.TenantId;
            var roleName = Authorization.NormalizeRoleName(request.Name);
            if (Authorization.IsBuiltInRole(roleName))
            {
                return Failure("BUILT_IN_ROLE");
            }

            var unknownPermissions = DataHubPermissionCatalog.GetUnknownPermissions(request.Permissions);
            if (unknownPermissions.Any())
            {
                return Failure($"UNKNOWN_PERMISSION:{string.Join(",", unknownPermissions)}");
            }

            var permissions = DataHubPermissionCatalog.NormalizePermissions(request.Permissions);
            var existingRole = await GetRoleAsync(tenantId, roleName, cancellationToken);
            if (existingRole != null)
            {
                return Failure("ROLE_EXISTS");
            }

            var role = new DataHubRole
            {
                id = idService.NewId<DataHubRole>(),
                TenantId = tenantId,
                Name = roleName,
                Description = request.Description,
                Permissions = permissions
            };

            var updateResponse = (await mediator.TrySend(new UpsertCosmosDocumentsCommand<DataHubRole>
            {
                Documents = [role]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (updateResponse.Failures.Any())
            {
                return Failure(string.Join("\n", updateResponse.Failures.Select(failure => $"{failure.Item.id}: {failure.Error.Message}")));
            }

            return new RegisterRoleResponse
            {
                Success = true,
                Result = await mapper.MapAsync<DataHubRoleDTO>(role, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
    }

    private async Task<DataHubRole> GetRoleAsync(string tenantId, string roleName, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<DataHubRole>
        {
            WhereClause = "x.TenantId = @tenantId and x.Name = @roleName",
            PageSize = 2,
            Parameters =
            [
                new QueryParameter("tenantId", tenantId),
                new QueryParameter("roleName", roleName)
            ]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return response.Results.Count == 1 ? response.Results.First() : null;
    }

    private static RegisterRoleResponse Failure(string reason)
    {
        return new RegisterRoleResponse { Success = false, FailureReason = reason };
    }

}
