using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessDeleteJobs;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteJob;

public class DeleteJobRequestHandler(IMediator mediator) : IHandler<DeleteJobRequest, DeleteJobResponse>
{
    public async Task<DeleteJobResponse> HandleAsync(DeleteJobRequest request, CancellationToken cancellationToken)
    {
        var deleteResponse = (await mediator.TrySend(new ProcessDeleteJobsRequest()
        {
            JobIds = [request.JobId]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!deleteResponse.Success)
        {
            return new DeleteJobResponse()
            {
                Success = false,
                FailureReason = deleteResponse.FailureReason
            };
        }

        return new DeleteJobResponse()
        {
            Success = true
        };
    }
}