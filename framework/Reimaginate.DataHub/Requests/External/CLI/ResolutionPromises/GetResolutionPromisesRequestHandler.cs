using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.ResolutionPromises;

public class GetResolutionPromisesRequestHandler(IMediator mediator) : IHandler<GetResolutionPromisesRequest, GetResolutionPromisesResponse>
{
    public async Task<GetResolutionPromisesResponse> HandleAsync(GetResolutionPromisesRequest request, CancellationToken cancellationToken)
    {
        var parameters = new List<QueryParameter>();
        var idParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(request.PromiseIds.Distinct().ToList(), "promiseId", parameters);
        var whereClause = $"x.{nameof(ResolutionPromise.id)} in ({string.Join(",", idParameterNames)})";

        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<ResolutionPromise>
        {
            WhereClause = whereClause,
            Parameters = parameters,
            PageSize = request.PromiseIds.Count
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var value } => value };

        var results = (response.Results ?? [])
            .Select(ResolutionPromiseResultMapper.ToResult)
            .ToList();

        return new GetResolutionPromisesResponse
        {
            Results = results,
            ResultCount = results.Count
        };
    }
}
