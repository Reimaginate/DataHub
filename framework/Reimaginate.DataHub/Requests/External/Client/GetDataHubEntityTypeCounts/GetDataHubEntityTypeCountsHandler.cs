using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntityTypeCounts;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;

using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntityTypeCounts;

public class GetDataHubEntityTypeCountsHandler(IMediator mediator) : IHandler<GetDataHubEntityTypeCountsRequest, GetDataHubEntityTypeCountsResponse>
{
    public async Task<GetDataHubEntityTypeCountsResponse> HandleAsync(GetDataHubEntityTypeCountsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = (await mediator.TrySend(new GetDataHubEntityTypeCountsQuery()
            {
                WhereClause = request.WhereClause,
                Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            return new GetDataHubEntityTypeCountsResponse()
            {
                Success = true,
                Results = response
            };
        }
        catch (Exception ex)
        {
            return new GetDataHubEntityTypeCountsResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
