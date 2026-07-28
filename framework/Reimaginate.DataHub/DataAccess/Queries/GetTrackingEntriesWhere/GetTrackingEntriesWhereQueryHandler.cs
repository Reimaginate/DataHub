using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesWhere;

public class GetTrackingEntriesWhereQueryHandler(IPartitionedDataService<ChangeTrackingEntry> dataService) : IHandler<GetTrackingEntriesWhereQuery, PagedResults<ChangeTrackingEntry>>
{
    public async Task<PagedResults<ChangeTrackingEntry>> HandleAsync(GetTrackingEntriesWhereQuery query, CancellationToken cancellationToken)
    {
        var results = DataHubQueryParameterMapper.HasParameters(query.Parameters)
            ? await dataService.PagedWhereParameterizedAsync(
                query.WhereClause,
                query.Parameters,
                pageSize: query.PageSize,
                continuationToken: query.ContinuationToken,
                select: query.Select,
                from: query.From ?? "x",
                orderBy: query.OrderBy,
                getTotalResultCount: query.GetTotalResultCount,
                cancellationToken: cancellationToken)
            : await dataService.PagedWhereAsync(
                query.WhereClause,
                pageSize: query.PageSize,
                continuationToken: query.ContinuationToken,
                select: query.Select,
                from: query.From ?? "x",
                orderBy: query.OrderBy,
                getTotalResultCount: query.GetTotalResultCount,
                cancellationToken: cancellationToken);

        return results;
    }
}
