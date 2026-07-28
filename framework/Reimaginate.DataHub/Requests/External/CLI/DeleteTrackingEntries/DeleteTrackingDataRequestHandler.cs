using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DeleteTrackingDataEntries;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteTrackingEntries;

public class DeleteTrackingDataRequestHandler(IMediator mediator) : IHandler<DeleteTrackingEntriesRequest, DeleteTrackingEntriesResponse>
{
    public async Task<DeleteTrackingEntriesResponse> HandleAsync(DeleteTrackingEntriesRequest request, CancellationToken cancellationToken)
    {
        var ret = (await mediator.TrySend(new DeleteTrackingDataEntriesRequest()
        {
            Ids = request.TrackingEntryIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        
        return new DeleteTrackingEntriesResponse()
        {
            Success =  ret.Success,
            Failures = ret.Failures
        };
    }
}