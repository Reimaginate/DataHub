using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteAlerts;

public class DeleteAlertsRequestHandler(IMediator mediator) : IHandler<DeleteAlertsRequest, DeleteAlertsResponse>
{
    public async Task<DeleteAlertsResponse> HandleAsync(DeleteAlertsRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new DeleteLogEntriesRequest
        {
            Ids = request.AlertIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new DeleteAlertsResponse
        {
            Success = response.Success,
            FailureReason = response.FailureReason,
            DeleteFailures = response.DeleteFailures
        };
    }
}
