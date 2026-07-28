using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.SubmitJob;

public class SubmitJobRequestHandler(IMediator mediator, ITimeService timeService) : IHandler<SubmitJobRequest, SubmitJobResponse>
{
    public async Task<SubmitJobResponse> HandleAsync(SubmitJobRequest request, CancellationToken cancellationToken)
    {
        var now = timeService.Now();
        
        var job = new JobDTO()
        {
            Type = request.Type,
            Name = request.Name,
            Target = request.Target,
            CreatedOn = now,
            LastUpdated = now,
            Request = request.Request,
            Response = request.Response,
            Status = !string.IsNullOrEmpty(request.Status) ? request.Status : JobsConstants.Statuses.Ready,
        };


        var processSubmitJobsRequest = new ProcessSubmitJobsRequest()
        {
            Jobs = [job],
            DisableNotifications = request.DisableNotifications
        };

        var processSubmitJobsResponse = (await mediator.TrySend(processSubmitJobsRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (!processSubmitJobsResponse.Success)
        {
            return new SubmitJobResponse()
            {
                Success = false,
                FailureReason = processSubmitJobsResponse.FailureReason
            };
        }

        var result = processSubmitJobsResponse.Results.FirstOrDefault();

        if (result == null)
        {
            return new SubmitJobResponse()
            {
                Success = false,
                FailureReason = "RESULT_WAS_NULL"
            };
        }

        if (!result.Success)
        {
            return new SubmitJobResponse()
            {
                Success = false,
                FailureReason = result.FailureReason,
                Result = result.Result
            };
        }

        if (string.IsNullOrEmpty(result.Result.JobId))
        {
            return new SubmitJobResponse()
            {
                Success = false,
                FailureReason = "NO_JOB_ID_RETURNED",
                Result = result.Result
            };
        }


        return new SubmitJobResponse()
        {
            Success = true,
            Result = result.Result
        };
    }
}
