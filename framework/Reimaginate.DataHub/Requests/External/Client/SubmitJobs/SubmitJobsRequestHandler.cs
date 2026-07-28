using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.SubmitJobs;

public class SubmitJobsRequestHandler(IMediator mediator) : IHandler<SubmitJobsRequest, SubmitJobsResponse>
{
    public async Task<SubmitJobsResponse> HandleAsync(SubmitJobsRequest request, CancellationToken cancellationToken)
    {
        var processSubmitJobsRequest = new ProcessSubmitJobsRequest()
        {
            Jobs = request.Jobs,
            DisableNotifications = request.DisableNotifications
        };

        var processSubmitJobsResponse = (await mediator.TrySend(processSubmitJobsRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new SubmitJobsResponse()
        {
            Success = true,
            Results = processSubmitJobsResponse.Results
        };
    }
}
