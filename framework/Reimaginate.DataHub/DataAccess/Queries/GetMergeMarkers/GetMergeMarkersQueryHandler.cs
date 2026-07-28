using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetMergeMarkers;

public class GetMergeMarkersQueryHandler(IServiceProvider serviceProvider) : IHandler<GetMergeMarkersQuery, List<MergeMarker>>
{
    private readonly IPartitionedDataService<MergeMarker> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<MergeMarker>>();

    public async Task<List<MergeMarker>> HandleAsync(GetMergeMarkersQuery query, CancellationToken cancellationToken)
    {
        const string where = "x._dt = @documentType and x.DataSource = @dataSource and x.EntityType = @entityType";

        var results = await _dataService.WhereParameterizedAsync(
                where,
                [
                    new QueryParameter("documentType", nameof(MergeMarker)),
                    new QueryParameter("dataSource", query.DataSource),
                    new QueryParameter("entityType", query.EntityType)
                ],
                cancellationToken: cancellationToken
            );

        return results.Results;
    }
}
