using System;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateJobs;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateJob;

public class UpdateJobRequestHandler(IMediator mediator) : IHandler<UpdateJobRequest, UpdateJobResponse>
{
    public async Task<UpdateJobResponse> HandleAsync(UpdateJobRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updateJobResponse = (await mediator.TrySend(new ProcessUpdateJobsRequest()
            {
                Jobs = [request.Job]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!updateJobResponse.Success)
            {
                return new UpdateJobResponse()
                {
                    Success = false,
                    FailureReason = updateJobResponse.FailureReason
                };

            }

            return new UpdateJobResponse()
            {
                Success = true
            };
        }
        catch(Exception ex)
        {
            return new UpdateJobResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }   
    }
}