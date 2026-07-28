using System;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.Requests.Internal.ProcessDeleteDuplicates;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.DeleteDuplicates;

public class DeleteDuplicatesRequestHandler(IMediator mediator) : IHandler<DeleteDuplicatesRequest, DeleteDuplicatesResponse>
{
    public async Task<DeleteDuplicatesResponse> HandleAsync(DeleteDuplicatesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var deleteResponse = (await mediator.TrySend(new ProcessDeleteDuplicatesRequest()
            {
                Where = request.Where,
                Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!deleteResponse.Success)
            {
                return new DeleteDuplicatesResponse()
                {
                    Success = false,
                    FailureReason = deleteResponse.FailureReason
                };
            }

            return new DeleteDuplicatesResponse()
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new DeleteDuplicatesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
