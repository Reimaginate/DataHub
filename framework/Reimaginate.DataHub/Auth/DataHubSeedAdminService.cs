using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Auth;

public class DataHubSeedAdminService(IMediator mediator, IIdService idService, IDataHubAuthorizationService authorizationService)
    : IDataHubSeedAdminService
{
    public async Task SeedAdminsAsync(IEnumerable<DataHubSeedAdminUser> users, CancellationToken cancellationToken = default)
    {
        foreach (var seedUser in users ?? [])
        {
            await SeedAdminAsync(seedUser, cancellationToken);
        }
    }

    private async Task SeedAdminAsync(DataHubSeedAdminUser seedUser, CancellationToken cancellationToken)
    {
        Validate(seedUser);

        var roles = NormalizeRoles(seedUser.Roles);
        if (!roles.Any())
        {
            roles = [DataHubRoles.Admin];
        }

        if (!await authorizationService.ValidateRoleReferencesAsync(seedUser.TenantId, roles, cancellationToken))
        {
            throw new InvalidOperationException($"Seed admin '{seedUser.UPN}' references an unknown DataHub role.");
        }

        var existingUsers = (await mediator.TrySend(new GetCosmosDocumentsQuery<User>
        {
            WhereClause = "x.TenantId = @tenantId and (x.EntraObjectId = @entraObjectId or x.UPN = @upn)",
            PageSize = 2,
            Parameters =
            [
                new QueryParameter("tenantId", seedUser.TenantId),
                new QueryParameter("entraObjectId", seedUser.EntraObjectId),
                new QueryParameter("upn", seedUser.UPN)
            ]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var user = existingUsers.Results.SingleOrDefault();
        if (user == null)
        {
            user = new User
            {
                id = idService.NewId<User>(),
                Disabled = false
            };
        }

        user.TenantId = seedUser.TenantId;
        user.EntraObjectId = seedUser.EntraObjectId;
        user.UPN = seedUser.UPN;
        user.Email = seedUser.Email;
        user.Name = seedUser.Name;
        user.Roles = roles;

        var updateResponse = (await mediator.TrySend(new UpsertCosmosDocumentsCommand<User>
        {
            Documents = [user]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (updateResponse.Failures.Any())
        {
            throw new InvalidOperationException(string.Join("\n", updateResponse.Failures.Select(failure => $"{failure.Item.id}: {failure.Error.Message}")));
        }
    }

    private static void Validate(DataHubSeedAdminUser user)
    {
        if (string.IsNullOrWhiteSpace(user?.TenantId))
        {
            throw new InvalidOperationException("Seed admin TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(user.EntraObjectId))
        {
            throw new InvalidOperationException("Seed admin EntraObjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(user.UPN))
        {
            throw new InvalidOperationException("Seed admin UPN is required.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("Seed admin Email is required.");
        }

        if (string.IsNullOrWhiteSpace(user.Name))
        {
            throw new InvalidOperationException("Seed admin Name is required.");
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
