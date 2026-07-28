using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntitiesWhere;

public class GetEntitiesWhereRequestHandler(IMediator mediator) : IHandler<GetDataHubEntitiesWhereRequest, GetDataHubEntitiesResponse>
{
    public async Task<GetDataHubEntitiesResponse> HandleAsync(GetDataHubEntitiesWhereRequest request, CancellationToken cancellationToken)
    {
        try
        {

            var whereClauses = new List<string> { request.WhereClause };

            var where = string.Join(" and ", whereClauses);

            var response = (await mediator.TrySend(new GetDataHubEntitiesQuery()
            {
                Select = request.Select,
                From = request.From,
                ContinuationToken = request.ContinuationToken,
                WhereClause = where,
                PageSize = request.PageSize,
                OrderBy = request.OrderBy,
                GetTotalResultCount = request.GetTotalResultCount,
                Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            return new GetDataHubEntitiesResponse()
            {
                Success = true,
                Results = response.Results,
                ContinuationToken = response.ContinuationToken,
                MoreResultsAvailable = response.MoreResultsAvailable,
                ResultCount = response.ResultCount
            };
        }
        catch (Exception ex)
        {
            return new GetDataHubEntitiesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
