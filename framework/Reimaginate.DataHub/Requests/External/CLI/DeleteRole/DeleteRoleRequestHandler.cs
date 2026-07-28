using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteRole;

public class DeleteRoleRequestHandler(IMediator mediator) : IHandler<DeleteRoleRequest, DeleteRoleResponse>
{
    public async Task<DeleteRoleResponse> HandleAsync(DeleteRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = request.User.TenantId;
            var roleName = Authorization.NormalizeRoleName(request.Name);
            if (Authorization.IsBuiltInRole(roleName))
            {
                return Failure("BUILT_IN_ROLE");
            }

            var role = await GetRoleAsync(tenantId, roleName, cancellationToken);
            if (role == null)
            {
                return Failure("NOT_FOUND");
            }

            if (await EnabledUserReferencesRoleAsync(tenantId, roleName, cancellationToken))
            {
                return Failure("ROLE_IN_USE");
            }

            var deleteResponse = (await mediator.TrySend(new DeleteCosmosDocumentsCommand<DataHubRole>
            {
                Documents = [role]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (deleteResponse.Failures.Any())
            {
                return Failure(string.Join("\n", deleteResponse.Failures.Select(failure => $"{failure.Item.id}: {failure.Error.Message}")));
            }

            return new DeleteRoleResponse { Success = true };
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

    private async Task<bool> EnabledUserReferencesRoleAsync(string tenantId, string roleName, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<User>
        {
            WhereClause = "x.TenantId = @tenantId and x.Disabled = false",
            Parameters = [new QueryParameter("tenantId", tenantId)]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return response.Results.Any(user => user.Roles?.Any(role => Authorization.NormalizeRoleName(role) == roleName) == true);
    }

    private static DeleteRoleResponse Failure(string reason)
    {
        return new DeleteRoleResponse { Success = false, FailureReason = reason };
    }

}
