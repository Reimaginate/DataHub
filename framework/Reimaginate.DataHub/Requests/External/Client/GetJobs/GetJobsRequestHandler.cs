using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.Requests.Internal.ProcessGetJobs;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetJobs;

public class GetJobsRequestHandler(IMediator mediator, IMapper mapper) : IHandler<GetJobsRequest, GetJobsResponse>
{
    public async Task<GetJobsResponse> HandleAsync(GetJobsRequest request, CancellationToken cancellationToken)
    {
        var getJobsResponse = (await mediator.TrySend(new ProcessGetJobsRequest()
        {
            Select = request.Select,
            Where = request.Where,
            OrderBy = request.OrderBy,
            PageSize = request.PageSize,
            GetTotalResultCount = request.GetTotalResultCount,
            ContinuationToken = request.ContinuationToken,
            Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!getJobsResponse.Success)
        {
            return new GetJobsResponse()
            {
                Success = false,
                FailureReason = getJobsResponse.FailureReason
            };
        }

        var mappedResults = await mapper.MapAsync<List<JobDTO>>(getJobsResponse.PagedResults.Results, cancellationToken);
       
        return new GetJobsResponse()
        {
            Success = true,
            Results = mappedResults,
            ContinuationToken = getJobsResponse.PagedResults.ContinuationToken,
            MoreResultsAvailable = !string.IsNullOrEmpty(getJobsResponse.PagedResults.ContinuationToken),
            ResultCount = getJobsResponse.PagedResults.ResultCount
        };
    }
}
