using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Requests.Internal.ProcessDeleteJobs;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteJobs;

public class DeleteJobsRequestHandler(IMediator mediator) : IHandler<DeleteJobsRequest, DeleteJobsResponse>
{
    public async Task<DeleteJobsResponse> HandleAsync(DeleteJobsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var getJobsRequest = new GetCosmosDocumentsQuery<Job>()
            {
                WhereClause = request.Where,
                Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
            };

            var getJobsResponse = (await mediator.TrySend(getJobsRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!getJobsResponse.Results.Any())
            {
                return new DeleteJobsResponse()
                {
                    Success = false,
                    FailureReason = "NOT_FOUND"
                };
            }

            var failures = new List<string>();

            while (getJobsResponse.Results.Any())
            {
                var jobsIdsToDelete = getJobsResponse.Results.Select(s => s.id).ToList();
                var deleteResponse = (await mediator.TrySend(new ProcessDeleteJobsRequest()
                {
                    JobIds = jobsIdsToDelete
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                if (!deleteResponse.Success)
                {
                    failures.Add(deleteResponse.FailureReason);
                }

                if (!getJobsResponse.MoreResultsAvailable || !deleteResponse.Success)
                {
                    break;
                }

                getJobsRequest.ContinuationToken = null;
                getJobsResponse = (await mediator.TrySend(getJobsRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            }

            if (failures.Any())
            {
                return new DeleteJobsResponse()
                {
                    Success = false,
                    FailureReason = string.Join("\n", failures)
                };

            }

            return new DeleteJobsResponse()
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new DeleteJobsResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
