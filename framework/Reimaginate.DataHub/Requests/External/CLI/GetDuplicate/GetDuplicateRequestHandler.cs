using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessGetDuplicates;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetDuplicate;

public class GetDuplicateRequestHandler(IMediator mediator) : IHandler<GetDuplicateRequest, GetDuplicateResponse>
{
    public async Task<GetDuplicateResponse> HandleAsync(GetDuplicateRequest request, CancellationToken cancellationToken)
    {
        var getDuplicatesRequest = new ProcessGetDuplicatesRequest()
        {
            Where = "x.id = @id",
            Parameters = [new QueryParameter("id", request.Id)]
        };
        var getDuplicatesResponse = (await mediator.TrySend(getDuplicatesRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!getDuplicatesResponse.Success)
        {
            return new GetDuplicateResponse()
            {
                Success = false,
                FailureReason = getDuplicatesResponse.FailureReason
            };
        }

        var result = getDuplicatesResponse.PagedResults.Results.FirstOrDefault();
        if (result == null)
        {
            if (getDuplicatesResponse.PagedResults.MoreResultsAvailable)
            {
                getDuplicatesRequest.ContinuationToken = getDuplicatesResponse.PagedResults.ContinuationToken;
                getDuplicatesResponse = (await mediator.TrySend(getDuplicatesRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                
                result = getDuplicatesResponse.PagedResults.Results.FirstOrDefault();
                if (result != null)
                {
                    return new GetDuplicateResponse()
                    {
                        Success = true,
                        Result = result
                    };
                }
            }

            return new GetDuplicateResponse()
            {
                Success = false,
                FailureReason = "NOT_FOUND"
            };
        }

        return new GetDuplicateResponse()
        {
            Success = true,
            Result = result
        };
    }
}
