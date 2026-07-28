using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;

public class CreateCosmosDocumentsCommandHandler<T>(IServiceProvider serviceProvider) : IHandler<CreateCosmosDocumentsCommand<T>, CreateCosmosDocumentsResponse<T>>
    where T : CosmosDocument
{
    private readonly IIdService _idService = serviceProvider.GetRequiredService<IIdService>();
    private readonly IPartitionedDataService<T> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<T>>();

    public async Task<CreateCosmosDocumentsResponse<T>> HandleAsync(CreateCosmosDocumentsCommand<T> request, CancellationToken cancellationToken)
    {
        request.Documents.ForEach(doc =>
        {
            doc.id ??= _idService.NewId<T>();
        });

        var result = await _dataService.BulkCreateItemsAsync(request.Documents, cancellationToken);
        return new CreateCosmosDocumentsResponse<T>()
        {
            Successes = result.Successes,
            Failures = result.Failures.Select(s => new DataAccessFailure<T>(s.Item, s.Error)).ToList()
        };
    }
}
