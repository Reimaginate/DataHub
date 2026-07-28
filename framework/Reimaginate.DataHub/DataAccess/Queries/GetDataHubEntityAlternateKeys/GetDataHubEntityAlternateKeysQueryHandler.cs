using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Polly;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntityAlternateKeys;

public class GetDataHubEntityAlternateKeysQueryHandler(IServiceProvider serviceProvider) : IHandler<GetDataHubEntityAlternateKeysQuery, PagedResults<JObject>>
{
    private readonly IDataHubEntityDataService _dataService = serviceProvider.GetRequiredService<IDataHubEntityDataService>();

    public async Task<PagedResults<JObject>> HandleAsync(GetDataHubEntityAlternateKeysQuery query, CancellationToken cancellationToken)
    {
        var where = $"x.{nameof(CosmosDocument._dt)}='{nameof(DataHubEntity)}'";
        if (!string.IsNullOrEmpty(query.WhereClause)) where += $" and {query.WhereClause}";

        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt));


        PagedResults<JObject> results = null;
        await retryPolicy.Execute(async () =>
        {
            results = await _dataService.PagedWhereAsync(
                where,
                pageSize: query.PageSize,
                continuationToken: query.ContinuationToken,
                select: "x.entityType as EntityType, x.id as EntityId, ak[\"Key\"] ,ak[\"Value\"]",
                from: "x join ak in x.alternateKeys",
                orderBy: query.OrderBy,
                getTotalResultCount: query.GetTotalResultCount,
                cancellationToken: cancellationToken
            );
        });

        return results;
    }
}