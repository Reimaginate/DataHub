using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.SubmitJobs;

public class SubmitJobsRequestHandler(IMediator mediator) : IHandler<SubmitJobsRequest, SubmitJobsResponse>
{
    public async Task<SubmitJobsResponse> HandleAsync(SubmitJobsRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new ProcessSubmitJobsRequest
        {
            Jobs = request.Jobs,
            User = request.User,
            DisableNotifications = request.DisableNotifications
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new SubmitJobsResponse
        {
            Success = response.Success,
            FailureReason = response.FailureReason,
            Results = response.Results
        };
    }
}
