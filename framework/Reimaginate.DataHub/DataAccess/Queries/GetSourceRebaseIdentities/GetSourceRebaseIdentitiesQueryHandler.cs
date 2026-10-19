using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetSourceRebaseIdentities;

public class GetSourceRebaseIdentitiesQueryHandler(IPartitionedDataService<ChangeTrackingEntry> dataService)
    : IHandler<GetSourceRebaseIdentitiesQuery, List<SourceRebaseIdentity>>
{
    public async Task<List<SourceRebaseIdentity>> HandleAsync(GetSourceRebaseIdentitiesQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query.EntityIds.Count == 0) return [];
        var parameters = new List<QueryParameter>
        {
            new("dataSource", query.DataSource), new("entityType", query.EntityType)
        };
        var idParameters = DataHubQueryParameterMapper.AddIndexedParameters(query.EntityIds, "entityId", parameters);
        // Alternate keys lose casing. Discover identity metadata only, then lock and
        // read history using exact stored pairs. DISTINCT object projections cannot expose
        // continuation tokens; WhereParameterizedAsync drains all database pages
        // internally, retaining only the distinct identity pairs, not tracking payloads.
        var response = await dataService.WhereParameterizedAsync(
            $"STRINGEQUALS(x.DataSource, @dataSource, true) AND STRINGEQUALS(x.EntityType, @entityType, true) AND x.EntityId IN ({string.Join(",", idParameters)})",
            parameters, select: "DISTINCT x.DataSource, x.EntityType", cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return response.Results.Where(entry => !string.IsNullOrEmpty(entry.DataSource) && !string.IsNullOrEmpty(entry.EntityType))
            .Select(entry => new SourceRebaseIdentity(entry.DataSource, entry.EntityType)).Distinct()
            .OrderBy(identity => identity.DataSource, StringComparer.Ordinal)
            .ThenBy(identity => identity.EntityType, StringComparer.Ordinal).ToList();
    }
}
