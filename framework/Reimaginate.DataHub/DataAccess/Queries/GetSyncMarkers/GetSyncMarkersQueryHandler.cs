using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetSyncMarkers;

public class GetSyncMarkersQueryHandler(IServiceProvider serviceProvider) : IHandler<GetSyncMarkersQuery, List<SyncMarker>>
{
    private readonly IPartitionedDataService<SyncMarker> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<SyncMarker>>();

    public async Task<List<SyncMarker>> HandleAsync(GetSyncMarkersQuery query, CancellationToken cancellationToken)
    {
        const string where = "x._dt = @documentType and x.DataSource = @dataSource and x.EntityType = @entityType";

        var results = await _dataService.WhereParameterizedAsync(
                where,
                [
                    new QueryParameter("documentType", nameof(SyncMarker)),
                    new QueryParameter("dataSource", query.DataSource),
                    new QueryParameter("entityType", query.EntityType)
                ],
                cancellationToken: cancellationToken
            );

        return results.Results;
    }
}
