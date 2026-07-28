using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;

public class UpsertCosmosDocumentsCommandHandler<T>(IServiceProvider serviceProvider) : IHandler<UpsertCosmosDocumentsCommand<T>, UpsertCosmosDocumentsResponse<T>>
    where T : CosmosDocument
{
    private readonly IIdService _idService = serviceProvider.GetRequiredService<IIdService>();
    private readonly IPartitionedDataService<T> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<T>>();

    public async Task<UpsertCosmosDocumentsResponse<T>> HandleAsync(UpsertCosmosDocumentsCommand<T> request, CancellationToken cancellationToken)
    {
        request.Documents.ForEach(doc =>
        {
            doc.id ??= _idService.NewId<T>();
        });

        var result = await _dataService.BulkUpsertItemsAsync(request.Documents, cancellationToken);

        var successes = result.Successes;
        var failures = result.Failures.Select(s => new DataAccessFailure<T>(s.Item, s.Error)).ToList();

        // Find documents that were submitted but not returned as successes or failures
        var returnedDocumentIds = new HashSet<string>(successes.Select(doc => doc.id).Concat(failures.Select(failure => failure.Item.id)));
        var missingDocuments = request.Documents.Where(doc => !returnedDocumentIds.Contains(doc.id));

        // Consider these documents as failures
        var missingFailures = missingDocuments.Select(missingDoc => new DataAccessFailure<T>(missingDoc, new Exception("Document was not returned as either success or failure.")));

        return new UpsertCosmosDocumentsResponse<T>()
        {
            Successes = successes,
            Failures = failures.Concat(missingFailures).ToList()
        };
    }

}
