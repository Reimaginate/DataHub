using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;

public class GetDataHubEntitiesQueryHandler(IServiceProvider serviceProvider) : IHandler<GetDataHubEntitiesQuery, PagedResults<JObject>>
{
    private readonly IDataHubEntityDataService _dataService = serviceProvider.GetRequiredService<IDataHubEntityDataService>();

    public async Task<PagedResults<JObject>> HandleAsync(GetDataHubEntitiesQuery query, CancellationToken cancellationToken)
    {
        var results = DataHubQueryParameterMapper.HasParameters(query.Parameters)
            ? await _dataService.PagedWhereParameterizedAsync(
                query.WhereClause,
                query.Parameters,
                pageSize: query.PageSize,
                continuationToken: query.ContinuationToken,
                select: query.Select,
                from: query.From ?? "x",
                orderBy: query.OrderBy,
                getTotalResultCount: query.GetTotalResultCount,
                cancellationToken: cancellationToken)
            : await _dataService.PagedWhereAsync(
                query.WhereClause,
                pageSize: query.PageSize,
                continuationToken: query.ContinuationToken,
                select: query.Select,
                from: query.From ?? "x",
                orderBy: query.OrderBy,
                getTotalResultCount: query.GetTotalResultCount,
                cancellationToken: cancellationToken);

        return results;
    }
}
