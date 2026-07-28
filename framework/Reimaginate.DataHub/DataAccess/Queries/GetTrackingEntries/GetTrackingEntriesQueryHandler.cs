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

namespace Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;

public class GetTrackingEntriesQueryHandler(IServiceProvider serviceProvider) : IHandler<GetTrackingEntriesQuery, PagedResults<ChangeTrackingEntry>>
{
    private readonly IPartitionedDataService<ChangeTrackingEntry> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<ChangeTrackingEntry>>();

    public async Task<PagedResults<ChangeTrackingEntry>> HandleAsync(GetTrackingEntriesQuery request, CancellationToken cancellationToken)
    {
        var whereComponents = new List<string>();
        var parameters = new List<QueryParameter>();

        if (!string.IsNullOrEmpty(request.DataSource))
        {
            whereComponents.Add("x.DataSource = @dataSource");
            parameters.Add(new QueryParameter("dataSource", request.DataSource));
        }

        if (!string.IsNullOrEmpty(request.EntityType))
        {
            whereComponents.Add("x.EntityType = @entityType");
            parameters.Add(new QueryParameter("entityType", request.EntityType));
        }

        if (request.FromDateTime.HasValue)
        {
            whereComponents.Add("x.Timestamp >= @fromDateTime");
            parameters.Add(new QueryParameter("fromDateTime", Uri.EscapeDataString(request.FromDateTime.Value.UtcDateTime.ToString(DateFormats.ISO8601))));
        }

        if (request.ToDateTime.HasValue)
        {
            whereComponents.Add("x.Timestamp <= @toDateTime");
            parameters.Add(new QueryParameter("toDateTime", Uri.EscapeDataString(request.ToDateTime.Value.UtcDateTime.ToString(DateFormats.ISO8601))));
        }

        if (request.EntityIds?.Any() ?? false)
        {
            var entityIdParameterNames = request.EntityIds
                .Select((entityId, index) =>
                {
                    var name = $"entityId{index}";
                    parameters.Add(new QueryParameter(name, entityId));
                    return $"@{name}";
                });
            whereComponents.Add($"x.EntityId in ({string.Join(",", entityIdParameterNames)})");
        }

        var where = whereComponents.Any() ? string.Join(" AND ", whereComponents) : string.Empty;

        var ret = DataHubQueryParameterMapper.HasParameters(parameters)
            ? await _dataService.PagedWhereParameterizedAsync(
                where,
                parameters,
                pageSize: request.PageSize ?? 100,
                continuationToken: request.ContinuationToken,
                getTotalResultCount: request.GetTotalResultCount,
                cancellationToken: cancellationToken)
            : await _dataService.PagedWhereAsync(
                where,
                pageSize: request.PageSize ?? 100,
                continuationToken: request.ContinuationToken,
                getTotalResultCount: request.GetTotalResultCount,
                cancellationToken: cancellationToken);

        return ret;
    }
}
