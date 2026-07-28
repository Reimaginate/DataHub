using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateDuplicates;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.UpdateDuplicates;

public class UpdateDuplicatesRequestHandler(IMediator mediator) : IHandler<UpdateDuplicatesRequest, UpdateDuplicatesResponse>
{
    public async Task<UpdateDuplicatesResponse> HandleAsync(UpdateDuplicatesRequest request, CancellationToken cancellationToken)
    {
        var updateDuplicatesResponse = (await mediator.TrySend(new ProcessUpdateDuplicatesRequest
        {
            Duplicates = request.Duplicates
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!updateDuplicatesResponse.Success)
        {
            return new UpdateDuplicatesResponse
            {
                Success = false,
                FailureReason = updateDuplicatesResponse.FailureReason
            };
        }

        return new UpdateDuplicatesResponse
        {
            Success = true
        };
    }
}
