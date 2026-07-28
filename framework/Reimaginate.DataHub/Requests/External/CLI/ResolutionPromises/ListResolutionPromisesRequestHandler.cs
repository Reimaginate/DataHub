using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.ResolutionPromises;

public class ListResolutionPromisesRequestHandler(IMediator mediator) : IHandler<ListResolutionPromisesRequest, ListResolutionPromisesResponse>
{
    public async Task<ListResolutionPromisesResponse> HandleAsync(ListResolutionPromisesRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<ResolutionPromise>
        {
            WhereClause = request.WhereClause,
            ContinuationToken = request.ContinuationToken,
            PageSize = request.PageSize,
            Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var value } => value };

        var results = (response.Results ?? [])
            .Select(ResolutionPromiseResultMapper.ToResult)
            .ToList();

        return new ListResolutionPromisesResponse
        {
            Results = results,
            ContinuationToken = response.ContinuationToken,
            MoreResultsAvailable = response.MoreResultsAvailable,
            ResultCount = response.ResultCount
        };
    }
}
