using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntityAlternateKeys;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetAlternateKeysWhere;

public class GetAlternateKeysWhereRequestHandler(IMediator mediator) : IHandler<GetAlternateKeysWhereRequest, GetAlternateKeysResponse>
{
    public async Task<GetAlternateKeysResponse> HandleAsync(GetAlternateKeysWhereRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new GetDataHubEntityAlternateKeysQuery()
        {
            ContinuationToken = request.ContinuationToken,
            WhereClause = request.WhereClause,
            PageSize = request.PageSize,
            OrderBy = request.OrderBy,
            GetTotalResultCount = request.GetTotalResultCount
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new GetAlternateKeysResponse()
        {
            Results = response.Results.Select(s=>s.ToObject<DataHubEntityAlternateKey>()).ToList(),
            ContinuationToken = response.ContinuationToken,
            MoreResultsAvailable = response.MoreResultsAvailable,
            ResultCount = response.ResultCount
        };

    }
}