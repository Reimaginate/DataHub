using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.Requests.Internal.ProcessGetDuplicates;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mapper;
using Reimaginate.Mediator;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Reimaginate.DataHub.Requests.External.Client.GetDuplicates;

public class GetDuplicatesRequestHandler(IMediator mediator, IMapper mapper) : IHandler<GetDuplicatesRequest, GetDuplicatesResponse>
{
    public async Task<GetDuplicatesResponse> HandleAsync(GetDuplicatesRequest request, CancellationToken cancellationToken)
    {
        var getDuplicatesResponse = (await mediator.TrySend(new ProcessGetDuplicatesRequest()
        {
            Select = request.Select,
            Where = request.Where,
            OrderBy = request.OrderBy,
            PageSize = request.PageSize,
            GetTotalResultCount = request.GetTotalResultCount,
            ContinuationToken = request.ContinuationToken,
            Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!getDuplicatesResponse.Success)
        {
            return new GetDuplicatesResponse()
            {
                Success = false,
                FailureReason = getDuplicatesResponse.FailureReason
            };
        }

        var mappedResults = await mapper.MapAsync<List<DuplicateDTO>>(getDuplicatesResponse.PagedResults.Results, cancellationToken);

        return new GetDuplicatesResponse()
        {
            Success = true,
            Results = mappedResults,
            ContinuationToken = getDuplicatesResponse.PagedResults.ContinuationToken,
            MoreResultsAvailable = !string.IsNullOrEmpty(getDuplicatesResponse.PagedResults.ContinuationToken),
            ResultCount = getDuplicatesResponse.PagedResults.ResultCount
        };
    }
}
