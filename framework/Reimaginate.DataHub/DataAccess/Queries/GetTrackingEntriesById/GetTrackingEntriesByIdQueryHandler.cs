using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesById;

public class GetTrackingEntriesByIdQueryHandler(IServiceProvider serviceProvider) : IHandler<GetTrackingEntriesByIdQuery, PagedResults<ChangeTrackingEntry>>
{
    private readonly IPartitionedDataService<ChangeTrackingEntry> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<ChangeTrackingEntry>>();

    public async Task<PagedResults<ChangeTrackingEntry>> HandleAsync(GetTrackingEntriesByIdQuery request, CancellationToken cancellationToken)
    {
        var parameters = new List<QueryParameter>
        {
            new("documentType", DocumentTypes.ChangeTrackingEntry)
        };
        var idParameterNames = request.Ids.Select((id, index) =>
        {
            var name = $"id{index}";
            parameters.Add(new QueryParameter(name, id));
            return $"@{name}";
        });
        var where = $"x.{nameof(DataHubEntity._dt)} = @documentType AND x.id in ({string.Join(",", idParameterNames)})";

        var ret = await _dataService
            .PagedWhereParameterizedAsync(
                where,
                parameters,
                pageSize: request.PageSize ?? 100,
                continuationToken: request.ContinuationToken,
                getTotalResultCount: request.GetTotalResultCount,
                cancellationToken: cancellationToken);

        return ret;
    }
}
