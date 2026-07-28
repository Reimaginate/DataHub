using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntitiesById;

public class GetDataHubEntitiesByIdRequestHandler(IMediator mediator) : IHandler<GetDataHubEntitiesByIdRequest, GetDataHubEntitiesByIdResponse>
{
    private const int QueryBatchSize = 5000;

    public async Task<GetDataHubEntitiesByIdResponse> HandleAsync(GetDataHubEntitiesByIdRequest request, CancellationToken cancellationToken)
    {
        try
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

            return new GetDataHubEntitiesByIdResponse()
            {
                Success = true,
                Results = results
            };
        }
        catch (Exception ex)
        {
            return new GetDataHubEntitiesByIdResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
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
            OrderBy = $"x.{nameof(DataHubEntity.entityType)}",
            PageSize = 1000,
            Parameters = parameters
        };
    }
}
