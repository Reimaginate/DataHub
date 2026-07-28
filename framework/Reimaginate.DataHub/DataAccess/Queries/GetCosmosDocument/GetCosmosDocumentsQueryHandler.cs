using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocument;

public class GetCosmosDocumentsQueryHandler<T>(IServiceProvider serviceProvider) : IHandler<GetCosmosDocumentQuery<T>, GetCosmosDocumentResponse<T>>
    where T : CosmosDocument
{
    private readonly IPartitionedDataService<T> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<T>>();

    public async Task<GetCosmosDocumentResponse<T>> HandleAsync(GetCosmosDocumentQuery<T> query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(query.Id))
        {
            return new GetCosmosDocumentResponse<T>()
            {
                Success = false,
                FailureReason = "MISSING_ID"
            };
        }

        var where = $"x.{nameof(CosmosDocument._dt)} = @__dataHubDocumentType and x.id = @__dataHubDocumentId";

        var response = await _dataService.PagedWhereParameterizedAsync<T>(
                where,
                [
                    new QueryParameter("__dataHubDocumentType", typeof(T).Name),
                    new QueryParameter("__dataHubDocumentId", query.Id)
                ],
                select: query.Select,
                cancellationToken: cancellationToken);

        if (!response.Results.Any())
        {
            return new GetCosmosDocumentResponse<T>()
            {
                Success = false,
                FailureReason = "NOT_FOUND"
            };
        }

        return new GetCosmosDocumentResponse<T>()
        {
            Success = true,
            Result = response.Results.First()
        };
    }
}
