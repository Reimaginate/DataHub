using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetEntitiesWhere;

public class GetEntitiesWhereRequestHandler(IMediator mediator) : IHandler<GetEntitiesWhereRequest, GetEntitiesResponse>
{
    public async Task<GetEntitiesResponse> HandleAsync(GetEntitiesWhereRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend( new GetDataHubEntitiesQuery()
        {
            Select = request.Select,
            From = request.From,
            ContinuationToken = request.ContinuationToken,
            WhereClause = request.WhereClause,
            PageSize = request.PageSize,
            OrderBy = request.OrderBy,
            GetTotalResultCount = request.GetTotalResultCount,
            Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new GetEntitiesResponse()
        {
            Success = true,
            Results = response.Results ?? [],
            ContinuationToken = response.ContinuationToken,
            MoreResultsAvailable = response.MoreResultsAvailable,
            ResultCount = response.ResultCount
        };
    }
}
