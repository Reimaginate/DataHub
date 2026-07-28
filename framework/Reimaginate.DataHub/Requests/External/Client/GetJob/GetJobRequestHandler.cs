using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessGetJobs;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetJob;

public class GetJobRequestHandler(IMediator mediator, IMapper mapper) : IHandler<GetJobRequest, GetJobResponse> 
{
    public async Task<GetJobResponse> HandleAsync(GetJobRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var getJobsResponse = (await mediator.TrySend(new ProcessGetJobsRequest()
            {
                Where = "x.id = @jobId",
                Parameters = [new QueryParameter("jobId", request.JobId)]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!getJobsResponse.Success)
            {
                return new GetJobResponse()
                {
                    Success = false,
                    FailureReason = getJobsResponse.FailureReason
                };
            }

            var result = getJobsResponse.PagedResults.Results.FirstOrDefault();
            if (result == null)
            {
                return new GetJobResponse()
                {
                    Success = false,
                    FailureReason = "NOT_FOUND"
                };
            }

            var ret = await mapper.MapAsync<JobDTO>(result, cancellationToken);

            return new GetJobResponse()
            {
                Success = true,
                Result = ret
            };
        }
        catch (Exception ex)
        {
            return new GetJobResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
