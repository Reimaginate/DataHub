using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

// ReSharper disable IdentifierTypo

namespace Reimaginate.DataHub.DataAccess.Commands.CreateDataHubEntities;

public class CreateDataHubEntitiesCommandHandler(IServiceProvider serviceProvider) : IHandler<CreateDataHubEntitiesCommand, CreateDataHubEntitiesResponse>
{
    private readonly IDataHubEntityDataService _dataService = serviceProvider.GetRequiredService<IDataHubEntityDataService>();

    private static void AddDocumentTypeProperty(JObject entity)
    {
        entity["_dt"] = nameof(DataHubEntity);
    }

    public async Task<CreateDataHubEntitiesResponse> HandleAsync(CreateDataHubEntitiesCommand request, CancellationToken cancellationToken)
    {
        request.Entities.ForEach(AddDocumentTypeProperty);

        var results = await _dataService.BulkCreateItemsAsync(request.Entities, cancellationToken);

        var successes = results.Successes;
        var failures = results.Failures.Select(s => new DataAccessFailure<JObject>(s.Item, s.Error)).ToList();

        return new CreateDataHubEntitiesResponse()
        {
            Successes = successes,
            Failures = failures
        };
    }
}