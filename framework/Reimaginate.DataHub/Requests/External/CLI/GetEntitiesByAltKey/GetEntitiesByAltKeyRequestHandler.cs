using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetEntitiesByAltKey;

public class GetEntitiesByAltKeyRequestHandler(IMediator mediator) : IHandler<GetEntitiesByAltKeyRequest, GetEntitiesResponse>
{
    public async Task<GetEntitiesResponse> HandleAsync(GetEntitiesByAltKeyRequest request, CancellationToken cancellationToken)
    {
        var parameters = new List<QueryParameter>();
        var keyPredicates = request.AlternateKeys.Select((altKey, index) =>
        {
            var keyParameterName = $"alternateKey{index}";
            var valueParameterName = $"alternateValue{index}";
            parameters.Add(new QueryParameter(keyParameterName, altKey.Key));
            parameters.Add(new QueryParameter(valueParameterName, altKey.Value));
            return $"(ak['{nameof(AlternateKey.Key)}'] = @{keyParameterName} and ak['{nameof(AlternateKey.Value)}'] = @{valueParameterName})";
        });

        var whereClause = $"exists (select ak from ak in x.{nameof(DataHubEntity.alternateKeys)} where {string.Join(" or ", keyPredicates)})";
        var response = (await mediator.TrySend(new GetDataHubEntitiesQuery
        {
            WhereClause = whereClause,
            PageSize = request.PageSize,
            ContinuationToken = request.ContinuationToken,
            Parameters = parameters
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new GetEntitiesResponse
        {
            Success = true,
            Results = response.Results ?? [],
            ContinuationToken = response.ContinuationToken,
            MoreResultsAvailable = response.MoreResultsAvailable,
            ResultCount = response.ResultCount
        };
    }

}
