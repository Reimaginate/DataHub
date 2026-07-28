using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocument;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.EnableUser;

public class EnableUserRequestHandler(IMediator mediator) : IHandler<SharedModels.Requests.CLI.EnableUserRequest, EnableUserResponse>
{
    public async Task<EnableUserResponse> HandleAsync(SharedModels.Requests.CLI.EnableUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var getUserResponse = (await mediator.TrySend(new GetCosmosDocumentQuery<User>()
            {
                Id = request.Id
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!getUserResponse.Success)
            {
                return new EnableUserResponse()
                {
                    Success = false,
                    FailureReason = getUserResponse.FailureReason
                };
            }

            var user = getUserResponse.Result;
            user.Disabled = false;

            var updateResponse = (await mediator.TrySend(new UpsertCosmosDocumentsCommand<User>()
            {
                Documents = [user]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (updateResponse.Failures.Any())
            {
                return new EnableUserResponse()
                {
                    Success = false,
                    FailureReason = string.Join("\n", updateResponse.Failures.Select(s => $"{s.Item.id}: {s.Error.Message}"))
                };
            }

            return new EnableUserResponse()
            {
                Success = true
            };

        }
        catch (Exception ex)
        {
            return new EnableUserResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}