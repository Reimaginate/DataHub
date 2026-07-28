using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;

public class GetCosmosDocumentsQueryHandler<T>(IServiceProvider serviceProvider) : IHandler<GetCosmosDocumentsQuery<T>, PagedResults<T>>
    where T : CosmosDocument
{
    private readonly IPartitionedDataService<T> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<T>>();

    public async Task<PagedResults<T>> HandleAsync(GetCosmosDocumentsQuery<T> query, CancellationToken cancellationToken)
    {
        var where = $"x.{nameof(CosmosDocument._dt)} = @__dataHubDocumentType";
        if (!string.IsNullOrEmpty(query.WhereClause)) where += $" and {query.WhereClause}";

        var parameters = DataHubQueryParameterMapper.Combine(
            [new QueryParameter("__dataHubDocumentType", typeof(T).Name)],
            query.Parameters);

        var results = await _dataService.PagedWhereParameterizedAsync<T>(
                where,
                parameters,
                select: query.Select,
                pageSize: query.PageSize,
                continuationToken: query.ContinuationToken,
                orderBy: query.OrderBy,
                getTotalResultCount: query.GetTotalResultCount,
                cancellationToken: cancellationToken);

        return results;
    }
}
