using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeletePatchFailures;

public class DeletePatchFailuresRequestHandler(IMediator mediator) : IHandler<DeletePatchFailuresRequest, DeletePatchFailuresResponse>
{
    public async Task<DeletePatchFailuresResponse> HandleAsync(DeletePatchFailuresRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new DeleteLogEntriesRequest()
        {
            Ids = request.PatchFailureIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new DeletePatchFailuresResponse()
        {
            Success = response.Success,
            FailureReason = response.FailureReason,
            DeleteFailures = response.DeleteFailures
        };
    }
}