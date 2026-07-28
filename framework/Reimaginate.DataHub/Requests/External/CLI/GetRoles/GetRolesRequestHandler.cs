using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetRoles;

public class GetRolesRequestHandler(IMediator mediator, IMapper mapper) : IHandler<GetRolesRequest, GetRolesResponse>
{
    public async Task<GetRolesResponse> HandleAsync(GetRolesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var builtInRoles = Authorization.BuiltInRoles
                .Select(role => new DataHubRoleDTO
                {
                    Name = role.Key,
                    Description = $"Built-in {role.Key} role",
                    BuiltIn = true,
                    Permissions = role.Value.OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase).ToList()
                })
                .ToList();

            var where = "x.TenantId = @tenantId";
            if (!string.IsNullOrWhiteSpace(request.Where))
            {
                where += $" and {request.Where}";
            }

            var getRolesResponse = (await mediator.TrySend(new GetCosmosDocumentsQuery<DataHubRole>
            {
                WhereClause = where,
                Parameters = DataHubQueryParameterMapper.Combine(
                    [new QueryParameter("tenantId", request.User.TenantId)],
                    DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters))
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var tenantRoles = await mapper.MapAsync<List<DataHubRoleDTO>>(getRolesResponse.Results, cancellationToken);
            tenantRoles.ForEach(role => role.BuiltIn = false);

            return new GetRolesResponse
            {
                Success = true,
                Results = builtInRoles.Concat(tenantRoles)
                    .OrderBy(role => role.BuiltIn ? 0 : 1)
                    .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            return new GetRolesResponse
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
