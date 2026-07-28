using System;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetJobs;

public class ProcessGetJobsRequestHandler(IMediator mediator) : IHandler<ProcessGetJobsRequest, ProcessGetJobsResponse>
{
    public async Task<ProcessGetJobsResponse> HandleAsync(ProcessGetJobsRequest request, CancellationToken cancellationToken)
    {
        var where = string.Empty;
        if (!string.IsNullOrEmpty(request.Where))
        {
            where = $"({request.Where})";
        }

        var req = new GetCosmosDocumentsQuery<Job>()
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
            return new ProcessGetJobsResponse()
            {
                Success = true,
                PagedResults = response
            };
        }
        catch (Exception ex)
        {
            return new ProcessGetJobsResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };

        }
    }
}
