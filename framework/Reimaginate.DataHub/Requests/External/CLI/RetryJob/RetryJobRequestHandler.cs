using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessGetJobs;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateJobs;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.RetryJob;

public class RetryJobRequestHandler(IMediator mediator, IMapper mapper, ITimeService timeService) : IHandler<RetryJobRequest, RetryJobResponse>
{
    public async Task<RetryJobResponse> HandleAsync(RetryJobRequest request, CancellationToken cancellationToken)
    {
        var getJobsResponse = (await mediator.TrySend(new ProcessGetJobsRequest
        {
            Where = "x.id = @jobId",
            Parameters = [new QueryParameter("jobId", request.JobId)]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!getJobsResponse.Success)
        {
            return new RetryJobResponse
            {
                Success = false,
                FailureReason = getJobsResponse.FailureReason
            };
        }

        var job = getJobsResponse.PagedResults.Results.FirstOrDefault();
        if (job == null)
        {
            return new RetryJobResponse
            {
                Success = false,
                FailureReason = "NOT_FOUND"
            };
        }

        var jobDto = await mapper.MapAsync<JobDTO>(job, cancellationToken);
        jobDto.Status = JobsConstants.Statuses.Ready;
        jobDto.CompletedOn = null;
        jobDto.LastUpdated = timeService.Now();

        var updateJobsResponse = (await mediator.TrySend(new ProcessUpdateJobsRequest
        {
            Jobs = [jobDto]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new RetryJobResponse
        {
            Success = updateJobsResponse.Success,
            FailureReason = updateJobsResponse.FailureReason
        };
    }
}
