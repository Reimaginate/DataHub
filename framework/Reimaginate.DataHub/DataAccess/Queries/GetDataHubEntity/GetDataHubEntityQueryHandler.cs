using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntity;

public class GetDataHubEntityQueryHandler(IServiceProvider serviceProvider) : IHandler<GetDataHubEntityQuery, JObject>
{
    private readonly IDataHubEntityDataService _dataService = serviceProvider.GetRequiredService<IDataHubEntityDataService>();

    public async Task<JObject> HandleAsync(GetDataHubEntityQuery request, CancellationToken cancellationToken)
    {
        var where = $"x.{nameof(CosmosDocument._dt)}='{nameof(DataHubEntity)}' and x.{nameof(DataHubEntity.id)} = '{request.Id}'";

        var results = await _dataService.WhereAsync(
                where,
                cancellationToken: cancellationToken
            );

        return results.Results.FirstOrDefault();
    }
}