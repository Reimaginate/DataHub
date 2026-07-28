using Reimaginate.Mediator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.RegisterUser;

public class RegisterUserRequestHandler(IMediator mediator, IIdService idService, IDataHubAuthorizationService authorizationService)
    : IHandler<SharedModels.Requests.CLI.RegisterUserRequest, RegisterUserResponse>
{
    public async Task<RegisterUserResponse> HandleAsync(SharedModels.Requests.CLI.RegisterUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            request.Roles = NormalizeRoles(request.Roles);
            if (!await authorizationService.ValidateRoleReferencesAsync(request.TenantId, request.Roles, cancellationToken))
            {
                return new RegisterUserResponse()
                {
                    Success = false,
                    FailureReason = "UNKNOWN_ROLE"
                };
            }

            var getExistingUserResponse = (await mediator.TrySend(new GetCosmosDocumentsQuery<User>()
            {
                WhereClause = "x.TenantId = @tenantId and (x.UPN = @upn or x.EntraObjectId = @entraObjectId)",
                Parameters =
                [
                    new QueryParameter("tenantId", request.TenantId),
                    new QueryParameter("upn", request.UPN),
                    new QueryParameter("entraObjectId", request.EntraObjectId)
                ]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (getExistingUserResponse.Results.Any())
            {
                return new RegisterUserResponse()
                {
                    Success = false,
                    FailureReason = "USER_EXISTS"
                };
            }

            var user = new User()
            {
                id = idService.NewId<User>(),
                Name = request.Name,
                Email = request.Email,
                Roles = request.Roles,
                UPN = request.UPN,
                EntraObjectId = request.EntraObjectId,
                Disabled = false,
                TenantId = request.TenantId
            };

            var updateResponse = (await mediator.TrySend(new UpsertCosmosDocumentsCommand<User>()
            {
                Documents = [user]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (updateResponse.Failures.Any())
            {
                return new RegisterUserResponse()
                {
                    Success = false,
                    FailureReason = string.Join("\n", updateResponse.Failures.Select(s => $"{s.Item.id}: {s.Error.Message}"))
                };
            }

            return new RegisterUserResponse()
            {
                Success = true
            };

        }
        catch (Exception ex)
        {
            return new RegisterUserResponse()
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
