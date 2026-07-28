using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterDuplicates;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterDuplicate;

public class RegisterDuplicateRequestHandler(IMediator mediator) : IHandler<RegisterDuplicateRequest, RegisterDuplicateResponse>
{
    public async Task<RegisterDuplicateResponse> HandleAsync(RegisterDuplicateRequest request, CancellationToken cancellationToken)
    {
        var registerDuplicateRequest = new ProcessRegisterDuplicatesRequest()
        {
            Duplicates = [request.Duplicate],
            AutoSubmitMergeJobs = request.AutoSubmitMergeJobs
        };

        var registerDuplicatesResponse = (await mediator.TrySend(registerDuplicateRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (!registerDuplicatesResponse.Success)
        {
            return new RegisterDuplicateResponse()
            {
                Success = false,
                FailureReason = registerDuplicatesResponse.FailureReason
            };
        }

        return new RegisterDuplicateResponse()
        {
            Success = true
        };
    }
}