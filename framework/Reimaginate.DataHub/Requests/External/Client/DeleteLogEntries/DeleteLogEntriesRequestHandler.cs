using System.Threading;
using System.Threading.Tasks;
using Reimaginate.Mediator;
using DeleteLogEntriesRequest = Reimaginate.DataHub.SharedModels.Requests.Client.DeleteLogEntriesRequest;
using DeleteLogEntriesResponse = Reimaginate.DataHub.SharedModels.Requests.Client.DeleteLogEntriesResponse;

namespace Reimaginate.DataHub.Requests.External.Client.DeleteLogEntries;

public class DeleteLogEntriesRequestHandler(IMediator mediator) : IHandler<DeleteLogEntriesRequest, DeleteLogEntriesResponse>
{
    public async Task<DeleteLogEntriesResponse> HandleAsync(DeleteLogEntriesRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new Internal.DeleteLogEntries.DeleteLogEntriesRequest()
        {
            Ids = request.Ids
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new DeleteLogEntriesResponse()
        {
            Success = response.Success,
            FailureReason = response.FailureReason
        };
    }
}