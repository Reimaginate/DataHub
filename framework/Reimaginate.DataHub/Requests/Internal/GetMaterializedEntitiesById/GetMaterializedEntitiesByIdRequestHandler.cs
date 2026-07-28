using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;

public class GetMaterializedEntitiesByIdRequestHandler(IMediator mediator) : IHandler<GetMaterializedEntitiesByIdRequest, GetMaterializedEntitiesByIdResponse>
{
    private const int QueryBatchSize = 5000;

    public async Task<GetMaterializedEntitiesByIdResponse> HandleAsync(GetMaterializedEntitiesByIdRequest request, CancellationToken cancellationToken)
    {
        var results = new List<JObject>();

        foreach (var entityIdBatch in request.EntityIds.Chunk(QueryBatchSize))
        {
            var query = CreateQuery(request.EntityType, entityIdBatch);
            var getEntitiesResponse = (await mediator.TrySend(query, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            results.AddRange(getEntitiesResponse.Results);

            while (getEntitiesResponse.MoreResultsAvailable)
            {
                query.ContinuationToken = getEntitiesResponse.ContinuationToken;
                getEntitiesResponse = (await mediator.TrySend(query, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                results.AddRange(getEntitiesResponse.Results);
            }
        }

        return new GetMaterializedEntitiesByIdResponse()
        {
            Results = results
        };
    }

    private static GetDataHubEntitiesQuery CreateQuery(string entityType, IEnumerable<string> entityIds)
    {
        var parameters = new List<QueryParameter>
        {
            new("entityType", entityType)
        };
        var idParameterNames = entityIds.Select((entityId, index) =>
        {
            var name = $"entityId{index}";
            parameters.Add(new QueryParameter(name, entityId));
            return $"@{name}";
        });

        return new GetDataHubEntitiesQuery
        {
            WhereClause = $"x.{nameof(DataHubEntity.entityType)} = @entityType and x.{nameof(DataHubEntity.id)} in ({string.Join(",", idParameterNames)})",
            PageSize = 5000,
            From = "x",
            Parameters = parameters
        };
    }
}
