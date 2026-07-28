using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntityTypeCounts;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetDataHubEntityTypeCounts;

public class GetDataHubEntityTypeCountsRequestHandler(IMediator mediator) : IHandler<GetDataHubEntityTypeCountsRequest, GetDataHubEntityTypeCountsResponse>
{
    public async Task<GetDataHubEntityTypeCountsResponse> HandleAsync(GetDataHubEntityTypeCountsRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new GetDataHubEntityTypeCountsQuery()
        {
            WhereClause = request.WhereClause,
            Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
       
        return new GetDataHubEntityTypeCountsResponse()
        {
            Results = response
        };
    }
}
