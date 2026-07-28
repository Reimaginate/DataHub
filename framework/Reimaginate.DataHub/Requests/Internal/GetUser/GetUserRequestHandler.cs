using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetUser;

public class GetUserRequestHandler(IMediator mediator) : IHandler<GetUserRequest, GetUserResponse>
{
    public async Task<GetUserResponse> HandleAsync(GetUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(request.EntraObjectId))
            {
                var objectIdResponse = await GetUserAsync(
                    $"x.TenantId = '{request.TenantId}' and x.EntraObjectId = '{request.EntraObjectId}'",
                    cancellationToken);

                if (objectIdResponse.Success || objectIdResponse.FailureReason != "NOT_FOUND")
                {
                    return objectIdResponse;
                }
            }

            return await GetUserAsync(
                $"x.TenantId = '{request.TenantId}' and x.UPN = '{request.UserId}'",
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new GetUserResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }

    private async Task<GetUserResponse> GetUserAsync(string whereClause, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<User>
        {
            WhereClause = whereClause
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (response.Results.Count > 1)
        {
            return new GetUserResponse()
            {
                Success = false,
                FailureReason = "MATCHED_MULTIPLE_USERS"
            };
        }

        if (response.Results.Count == 0)
        {
            return new GetUserResponse()
            {
                Success = false,
                FailureReason = "NOT_FOUND"
            };
        }

        return new GetUserResponse()
        {
            Success = true,
            Result = response.Results.First()
        };
    }
}
