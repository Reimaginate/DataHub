using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteJobs;

public class ProcessDeleteJobsRequestHandler(IMediator mediator) : IHandler<ProcessDeleteJobsRequest, ProcessDeleteJobsResponse>
{
    public async Task<ProcessDeleteJobsResponse> HandleAsync(ProcessDeleteJobsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new List<QueryParameter>();
            var jobIdParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(request.JobIds, "jobId", parameters);

            var getJobsRequest = new GetCosmosDocumentsQuery<Job>()
            {
                Select = "x.id",
                WhereClause = $"x.id in ({string.Join(",", jobIdParameterNames)})",
                Parameters = parameters
            };

            var getJobsResponse = (await mediator.TrySend(getJobsRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!getJobsResponse.Results.Any())
            {
                return new ProcessDeleteJobsResponse()
                {
                    Success = true
                };
            }

            var deleteFailures = new List<DataAccessFailure<Job>>();
            while (getJobsResponse.Results.Any())
            {
                var deleteRequest = new DeleteCosmosDocumentsCommand<Job>()
                {
                    Documents = getJobsResponse.Results
                };

                var deleteResponse = (await mediator.TrySend(deleteRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                deleteFailures.AddRange(deleteResponse.Failures);

                if (!getJobsResponse.MoreResultsAvailable || deleteResponse.Successes?.Any() != true || deleteResponse.Failures?.Any() == true)
                {
                    break;
                }

                getJobsRequest.ContinuationToken = null;
                getJobsResponse = (await mediator.TrySend(getJobsRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            }

            if (deleteFailures.Any())
            {

                return new ProcessDeleteJobsResponse()
                {
                    Success = false,
                    FailureReason = string.Join("\n", deleteFailures.Select(s => $"{s.Item.id}: {s.Error.Message}"))
                };
            }


            return new ProcessDeleteJobsResponse()
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new ProcessDeleteJobsResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
