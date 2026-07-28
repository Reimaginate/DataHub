using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateDuplicates;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateDuplicate;

public class UpdateDuplicateRequestHandler(IMediator mediator) : IHandler<UpdateDuplicateRequest, UpdateDuplicateResponse>
{
    public async Task<UpdateDuplicateResponse> HandleAsync(UpdateDuplicateRequest request, CancellationToken cancellationToken)
    {
        
        var updateDuplicateRequest = new ProcessUpdateDuplicatesRequest()
        {
            Duplicates = [request.Duplicate]
        };

        var updateDuplicatesResponse = (await mediator.TrySend(updateDuplicateRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (!updateDuplicatesResponse.Success)
        {
            return new UpdateDuplicateResponse()
            {
                Success = false,
                FailureReason = updateDuplicatesResponse.FailureReason
            };
        }

        return new UpdateDuplicateResponse()
        {
            Success = true,
        };
    }
}