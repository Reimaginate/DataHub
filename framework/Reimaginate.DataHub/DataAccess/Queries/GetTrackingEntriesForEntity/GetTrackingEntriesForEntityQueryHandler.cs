using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesForEntity;

public class GetTrackingEntriesForEntityQueryHandler(IServiceProvider serviceProvider) : IHandler<GetTrackingEntriesForEntityQuery, List<ChangeTrackingEntry>>
{
    private readonly IPartitionedDataService<ChangeTrackingEntry> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<ChangeTrackingEntry>>();

    public async Task<List<ChangeTrackingEntry>> HandleAsync(GetTrackingEntriesForEntityQuery request, CancellationToken cancellationToken)
    {
        var where = $"x.{nameof(DataHubEntity._dt)} = @documentType and x.DataSource = @dataSource and x.EntityType = @entityType and x.EntityId = @entityId";

        var response = await _dataService.WhereParameterizedAsync(
            where,
            [
                new QueryParameter("documentType", DocumentTypes.ChangeTrackingEntry),
                new QueryParameter("dataSource", request.DataSource),
                new QueryParameter("entityType", request.EntityType),
                new QueryParameter("entityId", request.EntityId)
            ],
            from: "x",
            cancellationToken: cancellationToken);
        return response.Results;
    }
}
