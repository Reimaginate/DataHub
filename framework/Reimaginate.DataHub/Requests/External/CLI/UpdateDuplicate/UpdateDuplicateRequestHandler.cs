using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateDuplicates;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.UpdateDuplicate;

public class UpdateDuplicateRequestHandler(IMediator mediator) : IHandler<UpdateDuplicateRequest, UpdateDuplicateResponse>
{
    public async Task<UpdateDuplicateResponse> HandleAsync(UpdateDuplicateRequest request, CancellationToken cancellationToken)
    {
        var updateDuplicatesResponse = (await mediator.TrySend(new ProcessUpdateDuplicatesRequest
        {
            Duplicates = [request.Duplicate]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!updateDuplicatesResponse.Success)
        {
            return new UpdateDuplicateResponse
            {
                Success = false,
                FailureReason = updateDuplicatesResponse.FailureReason
            };
        }

        return new UpdateDuplicateResponse
        {
            Success = true
        };
    }
}
