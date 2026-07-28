using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.RegisterJobs;

public class RegisterJobsRequestHandler(IMapper mapper, IMediator mediator, ITimeService timeService) : IHandler<RegisterJobsRequest, RegisterJobsResponse>
{
    public async Task<RegisterJobsResponse> HandleAsync(RegisterJobsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var now = timeService.Now();
            
            var jobs = request.JobRequests.Select(r => new Job()
            {
                id = r.JobId,
                Name = r.JobName,
                createdOn = now,
                CreatedBy = r.User != null ? new UserRef()
                {
                    Id = r.User.id,
                    Name = r.User.Name
                } : null,
                lastUpdated = now,
                Status = !string.IsNullOrEmpty(r.Status) ? r.Status : JobsConstants.Statuses.Ready,
                Target = r.Target,
                Type = r.JobType,
                Request = r.Request,
            }).ToList();

            var createJobsResponse = (await mediator.TrySend(new CreateCosmosDocumentsCommand<Job>()
            {
                Documents = jobs
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            
            var successes = await mapper.MapAsync<List<JobDTO>>(createJobsResponse.Successes, cancellationToken);

            var results = successes.Select(job => new RegisterJobResult()
            {
                Job = job,
                Success = true
            }).ToList();

            var failures = createJobsResponse.Failures.Select(failure => new RegisterJobResult()
            {
                Job = null,
                Success = false,
                FailureReason = failure.Error?.Message
            }).ToList();

            results.AddRange(failures);

            if (results.Any(a => !a.Success))
            {
                return new RegisterJobsResponse()
                {
                    Success = false,
                    FailureReason = "FAILED_TO_REGISTER_JOBS",
                    Results = results
                };
            }

            return new RegisterJobsResponse()
            {
                Success = true,
                Results = results
            };
        }
        catch (Exception ex)
        {
            return new RegisterJobsResponse()
            {
                Success = false,
                FailureReason = ex.Message,
            };
        }
    }
}
