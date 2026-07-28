using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.FindMatchingEntities;

public sealed class FindMatchingEntitiesQueryHandler(IServiceProvider serviceProvider) : IHandler<FindMatchingEntitiesQuery, JArray>
{
    private readonly IDataHubEntityDataService _dataService = serviceProvider.GetRequiredService<IDataHubEntityDataService>();

    public async Task<JArray> HandleAsync(FindMatchingEntitiesQuery request, CancellationToken cancellationToken)
    {
        var where = $"x.{nameof(CosmosDocument._dt)} = @__dataHubDocumentType and x.{nameof(DataHubEntity.entityType)} = @__dataHubEntityType and ({request.WhereClause})";
        var parameters = DataHubQueryParameterMapper.Combine(
            [
                new QueryParameter("__dataHubDocumentType", nameof(DataHubEntity)),
                new QueryParameter("__dataHubEntityType", request.EntityType)
            ],
            request.Parameters);

        var response = await _dataService.WhereParameterizedAsync(
            where,
            parameters,
            request.SelectClause,
            cancellationToken: cancellationToken);

        var results = response.Results;

        return new JArray(results);
    }
}
