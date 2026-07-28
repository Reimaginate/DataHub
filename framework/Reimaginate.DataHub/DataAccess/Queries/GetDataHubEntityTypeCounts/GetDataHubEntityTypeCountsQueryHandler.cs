using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntityTypeCounts;

public class GetDataHubEntityTypeCountsQueryHandler(IServiceProvider serviceProvider) : IHandler<GetDataHubEntityTypeCountsQuery, List<EntityTypeCount>>
{
    private readonly IDataHubEntityDataService _dataService = serviceProvider.GetRequiredService<IDataHubEntityDataService>();

    public async Task<List<EntityTypeCount>> HandleAsync(GetDataHubEntityTypeCountsQuery query, CancellationToken cancellationToken)
    {
        var cosmosQuery = $"SELECT x.entityType, COUNT(x) AS count FROM {query.From}";
        if (!string.IsNullOrEmpty(query.WhereClause)) cosmosQuery += " where " + query.WhereClause;
        cosmosQuery += " GROUP BY x.entityType";

        var results = DataHubQueryParameterMapper.HasParameters(query.Parameters)
            ? await _dataService.ExecuteParameterizedQueryAsync(cosmosQuery, query.Parameters, cancellationToken)
            : await _dataService.ExecuteQueryAsync(cosmosQuery, cancellationToken);
        var ret = results.Select(JObject.FromObject).Select(s => s.ToObject<EntityTypeCount>()).ToList();
        return ret;
    }
}
