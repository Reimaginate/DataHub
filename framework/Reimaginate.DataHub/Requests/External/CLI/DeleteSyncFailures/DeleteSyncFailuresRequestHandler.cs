using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteSyncFailures;

public class DeleteSyncFailuresRequestHandler(IMediator mediator) : IHandler<DeleteSyncFailuresRequest, DeleteSyncFailuresResponse>
{
    public async Task<DeleteSyncFailuresResponse> HandleAsync(DeleteSyncFailuresRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new DeleteLogEntriesRequest()
        {
            Ids = request.SyncFailureIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new DeleteSyncFailuresResponse()
        {
            Success = response.Success,
            DeleteFailures = response.DeleteFailures
        };
    }
}