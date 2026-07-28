using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocument;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DisableUser;

public class DisableUserRequestHandler(IMediator mediator) : IHandler<DisableUserRequest, DisableUserResponse>
{
    public async Task<DisableUserResponse> HandleAsync(DisableUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var getUserResponse = (await mediator.TrySend(new GetCosmosDocumentQuery<User>()
            {
                Id = request.Id
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!getUserResponse.Success)
            {
                return new DisableUserResponse()
                {
                    Success = false,
                    FailureReason = getUserResponse.FailureReason
                };
            }

            var user = getUserResponse.Result;
            user.Disabled = true;

            var updateResponse = (await mediator.TrySend(new UpsertCosmosDocumentsCommand<User>()
            {
                Documents = [user]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (updateResponse.Failures.Any())
            {
                return new DisableUserResponse()
                {
                    Success = false,
                    FailureReason = string.Join("\n", updateResponse.Failures.Select(s => $"{s.Item.id}: {s.Error.Message}"))
                };
            }

            return new DisableUserResponse()
            {
                Success = true
            };

        }
        catch (Exception ex)
        {
            return new DisableUserResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}