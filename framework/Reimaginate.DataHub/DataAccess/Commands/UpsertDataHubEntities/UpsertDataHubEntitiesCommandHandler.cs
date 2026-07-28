using System.Collections.Generic;
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

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;

public class UpsertDataHubEntitiesCommandHandler(IServiceProvider serviceProvider) : IHandler<UpsertDataHubEntitiesCommand, UpsertDataHubEntitiesResponse>
{
    private readonly IDataHubEntityDataService _dataService = serviceProvider.GetRequiredService<IDataHubEntityDataService>();

    private static void AddDataTypeProperty(JObject entity)
    {
        entity["_dt"] = nameof(DataHubEntity);
    }

    public async Task<UpsertDataHubEntitiesResponse> HandleAsync(UpsertDataHubEntitiesCommand request, CancellationToken cancellationToken)
    {
        request.Entities.ForEach(AddDataTypeProperty);

        var results = await _dataService.BulkUpsertItemsAsync(request.Entities, cancellationToken);

        var successes = results.Successes;
        var failures = results.Failures.Select(s => new DataAccessFailure<JObject>(s.Item, s.Error)).ToList();

        // Find documents that were submitted but not returned as successes or failures
        var returnedDocumentIds = new HashSet<string>(successes.Select(doc => doc.Value<string>(nameof(CosmosDocument.id))).Concat(failures.Select(failure => failure.Item.Value<string>(nameof(CosmosDocument.id)))));
        var missingDocuments = request.Entities.Where(doc => !returnedDocumentIds.Contains(doc.Value<string>(nameof(CosmosDocument.id))));

        // Consider these documents as failures
        var missingFailures = missingDocuments.Select(missingDoc => new DataAccessFailure<JObject>(missingDoc, new Exception("Document was not returned as either success or failure.")));

        return new UpsertDataHubEntitiesResponse()
        {
            Successes = successes,
            Failures = failures.Concat(missingFailures).ToList()
        };
    }
}
