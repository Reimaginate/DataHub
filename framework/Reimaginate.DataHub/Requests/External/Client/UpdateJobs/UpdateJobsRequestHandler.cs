using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateJobs;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateJobs;

public class UpdateJobsRequestHandler(IMediator mediator) : IHandler<UpdateJobsRequest, UpdateJobsResponse> 
{
    public async Task<UpdateJobsResponse> HandleAsync(UpdateJobsRequest request, CancellationToken cancellationToken)
    {
        var updateJobResponse = (await mediator.TrySend(new ProcessUpdateJobsRequest()
        {
            Jobs = request.Jobs
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!updateJobResponse.Success)
        {
            return new UpdateJobsResponse()
            {
                Success = false,
                FailureReason = updateJobResponse.FailureReason
            };
        }
        return new UpdateJobsResponse()
        {
            Success = true
        };
    }
}