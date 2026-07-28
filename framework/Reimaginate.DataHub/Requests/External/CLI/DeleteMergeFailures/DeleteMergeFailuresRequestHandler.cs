using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteMergeFailures;

public class DeleteMergeFailuresRequestHandler(IMediator mediator) : IHandler<DeleteMergeFailuresRequest, DeleteMergeFailuresResponse>
{
    public async Task<DeleteMergeFailuresResponse> HandleAsync(DeleteMergeFailuresRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new DeleteLogEntriesRequest()
        {
            Ids = request.MergeFailureIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new DeleteMergeFailuresResponse()
        {
            Success = response.Success,
            FailureReason = response.FailureReason,
            DeleteFailures = response.DeleteFailures
        };
    }
}