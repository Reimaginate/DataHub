using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.UpdateUser;

public class UpdateUserRequestHandler(IMediator mediator, IDataHubAuthorizationService authorizationService)
    : IHandler<UpdateUserRequest, UpdateUserResponse>
{
    public async Task<UpdateUserResponse> HandleAsync(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            request.Roles = NormalizeRoles(request.Roles);
            if (!await authorizationService.ValidateRoleReferencesAsync(request.TenantId, request.Roles, cancellationToken))
            {
                return new UpdateUserResponse()
                {
                    Success = false,
                    FailureReason = "UNKNOWN_ROLE"
                };
            }

            var getExistingUserResponse = (await mediator.TrySend(new GetCosmosDocumentsQuery<User>()
            {
                WhereClause = "x.id = @id",
                Parameters = [new QueryParameter("id", request.Id)]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!getExistingUserResponse.Results.Any())
            {
                return new UpdateUserResponse()
                {
                    Success = false,
                    FailureReason = "NOT_FOUND"
                };
            }

            var user = getExistingUserResponse.Results.First();

            user.Name = request.Name;
            user.Email = request.Email;
            user.Roles = request.Roles;
            user.UPN = request.UPN;
            user.TenantId = request.TenantId;
            user.EntraObjectId = request.EntraObjectId;


            var updateResponse = (await mediator.TrySend(new UpsertCosmosDocumentsCommand<User>()
            {
                Documents = [user]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (updateResponse.Failures.Any())
            {
                return new UpdateUserResponse()
                {
                    Success = false,
                    FailureReason = string.Join("\n", updateResponse.Failures.Select(s => $"{s.Item.id}: {s.Error.Message}"))
                };
            }

            return new UpdateUserResponse()
            {
                Success = true
            };

        }
        catch (Exception ex)
        {
            return new UpdateUserResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }

    private static List<string> NormalizeRoles(IEnumerable<string> roles)
    {
        return roles?
            .Select(Authorization.NormalizeRoleName)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }
}
