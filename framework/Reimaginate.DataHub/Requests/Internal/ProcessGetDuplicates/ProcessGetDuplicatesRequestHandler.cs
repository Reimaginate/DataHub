using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetDuplicates;

public class ProcessGetDuplicatesRequestHandler(IMediator mediator) : IHandler<ProcessGetDuplicatesRequest, ProcessGetDuplicatesResponse>
{
    public async Task<ProcessGetDuplicatesResponse> HandleAsync(ProcessGetDuplicatesRequest request, CancellationToken cancellationToken)
    {
        var where = string.Empty;
        if (!string.IsNullOrEmpty(request.Where))
        {
            where = $"({request.Where})";
        }

        var req = new GetCosmosDocumentsQuery<Duplicate>()
        {
            Select = request.Select,
            WhereClause = where,
            ContinuationToken = request.ContinuationToken,
            GetTotalResultCount = request.GetTotalResultCount,
            OrderBy = request.OrderBy,
            PageSize = request.PageSize ?? 100,
            Parameters = request.Parameters
        };

        try
        {
            var response = (await mediator.TrySend(req, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            return new ProcessGetDuplicatesResponse()
            {
                Success = true,
                PagedResults = response
            };
        }
        catch (Exception ex)
        {
            return new ProcessGetDuplicatesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };

        }
    }
}
