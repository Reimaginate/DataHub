using System;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetUsers;

public class ProcessGetUsersRequestHandler(IMediator mediator) : IHandler<ProcessGetUsersRequest, ProcessGetUsersResponse>
{
    public async Task<ProcessGetUsersResponse> HandleAsync(ProcessGetUsersRequest request, CancellationToken cancellationToken)
    {
        var where = string.Empty;
        if (!string.IsNullOrEmpty(request.Where))
        {
            where = request.Where;
        }

        var req = new GetCosmosDocumentsQuery<User>()
        {
            WhereClause = where,
            ContinuationToken = request.ContinuationToken,
            GetTotalResultCount = true,
            Parameters = request.Parameters
        };

        try
        {
            var response = (await mediator.TrySend(req, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            return new ProcessGetUsersResponse()
            {
                Success = true,
                PagedResults = response
            };
        }
        catch (Exception ex)
        {
            return new ProcessGetUsersResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };

        }
    }
}
