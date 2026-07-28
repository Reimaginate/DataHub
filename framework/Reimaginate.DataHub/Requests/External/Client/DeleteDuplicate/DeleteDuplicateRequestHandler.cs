using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessDeleteDuplicates;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.DeleteDuplicate;

public class DeleteDuplicateRequestHandler(IMediator mediator) : IHandler<DeleteDuplicateRequest, DeleteDuplicateResponse>
{
    public async Task<DeleteDuplicateResponse> HandleAsync(DeleteDuplicateRequest request, CancellationToken cancellationToken)
    {
        var deleteResponse = (await mediator.TrySend(new ProcessDeleteDuplicatesRequest()
        {
            Where = "x.id = @id",
            Parameters = [new QueryParameter("id", request.Id)]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!deleteResponse.Success)
        {
            return new DeleteDuplicateResponse()
            {
                Success = false,
                FailureReason = deleteResponse.FailureReason
            };
        }

        return new DeleteDuplicateResponse()
        {
            Success = true
        };
    }
}
