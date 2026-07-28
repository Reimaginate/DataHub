using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.Requests.Internal.ProcessGetDuplicates;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetDuplicates;

public class GetDuplicatesRequestHandler(IMediator mediator) : IHandler<GetDuplicatesRequest, GetDuplicatesResponse>
{
    public async Task<GetDuplicatesResponse> HandleAsync(GetDuplicatesRequest job, CancellationToken cancellationToken)
    {
        var getDuplicatesResponse = (await mediator.TrySend(new ProcessGetDuplicatesRequest()
        {
            Where = job.Where,
            ContinuationToken = job.ContinuationToken,
            PageSize = job.PageSize,
            GetTotalResultCount = job.GetTotalResultCount,
            Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(job.Parameters)
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!getDuplicatesResponse.Success)
        {
            return new GetDuplicatesResponse()
            {
                Success = false,
                FailureReason = getDuplicatesResponse.FailureReason
            };
        }

        var pagedResults = getDuplicatesResponse.PagedResults;
        
        return new GetDuplicatesResponse()
        {
            Success = true,
            Results = pagedResults.Results,
            ContinuationToken = pagedResults.ContinuationToken,
            MoreResultsAvailable = pagedResults.MoreResultsAvailable,
            ResultCount = pagedResults.ResultCount
        };
    }
}
