using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.Requests.Internal.DispatchJobs;
using Reimaginate.DataHub.Requests.Internal.RegisterJobs;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;

public class ProcessSubmitJobsRequestHandler(IMediator mediator, IIdService idService) : IHandler<ProcessSubmitJobsRequest, ProcessSubmitJobsResponse>
{
    public async Task<ProcessSubmitJobsResponse> HandleAsync(ProcessSubmitJobsRequest request, CancellationToken cancellationToken)
    {
        var jobDtos = request.Jobs;
        foreach (var jobDto in jobDtos)
        {
            jobDto.JobId = string.IsNullOrEmpty(jobDto.JobId) ? idService.NewId<Job>() : jobDto.JobId;
        }

        var resultsDic = new Dictionary<string, SubmitJobResult>();

        var jobs = jobDtos.Select(job => new RegisterJobRequest()
        {
            JobName = job.Name,
            JobId = job.JobId,
            JobType = job.Type,
            Target = job.Target,
            User = request.User,
            Request = job.Request,
            Status = job.Status,
        }).ToList();

        var registerJobsResponse = (await mediator.TrySend(new RegisterJobsRequest(jobs), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        registerJobsResponse.Results.ForEach(e =>
        {
            if (e.Job != null)
            {
                DataHubTelemetry.RecordJobStatus(e.Job.Status, e.Job.Type, e.Job.Target);
            }

            var result = new SubmitJobResult()
            {
                Success = e.Success,
                FailureReason = e.FailureReason,
                Result = e.Job
            };
            resultsDic[e.Job.JobId] = result;
        });

        var successes = registerJobsResponse.Results.Where(result => result.Success).ToList();

        if (!request.DisableNotifications)
        {
            var dispatchJobsRequest = new DispatchJobsRequest()
            {
                Jobs = successes.Select(s => s.Job).ToList()
            };

            var response = (await mediator.TrySend(dispatchJobsRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            var failures = response.Results.Where(result => !result.Success).ToList();

            foreach (var failure in failures)
            {
                resultsDic[failure.Result.JobId] = new SubmitJobResult()
                {
                    Success = false,
                    FailureReason = failure.FailureReason,
                    Result = failure.Result
                };
            }
        }

        return new ProcessSubmitJobsResponse()
        {
            Success = true,
            Results = resultsDic.Values.ToList()
        };
    }
}
