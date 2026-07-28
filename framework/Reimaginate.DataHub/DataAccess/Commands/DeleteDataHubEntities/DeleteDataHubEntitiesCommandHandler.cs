using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.DeleteDataHubEntities;

public class DeleteDataHubEntitiesCommandHandler(IServiceProvider serviceProvider) : IHandler<DeleteDataHubEntitiesCommand, DeleteDataHubEntitiesResponse>
{
    private readonly IDataHubEntityDataService _dataService = serviceProvider.GetRequiredService<IDataHubEntityDataService>();

    public async Task<DeleteDataHubEntitiesResponse> HandleAsync(DeleteDataHubEntitiesCommand request, CancellationToken cancellationToken)
    {
        var response = await _dataService.BulkDeleteItemsAsync(request.Entities, cancellationToken);
        return new DeleteDataHubEntitiesResponse()
        {
            Successes = response.Successes,
            Failures = response.Failures.Select(s => new DataAccessFailure<JObject>(s.Item, s.Error)).ToList()
        };
    }
}
