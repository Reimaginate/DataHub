using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterDuplicates;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterDuplicates;

public class RegisterDuplicatesRequestHandler(IMediator mediator) : IHandler<RegisterDuplicatesRequest, RegisterDuplicatesResponse>
{
    public async Task<RegisterDuplicatesResponse> HandleAsync(RegisterDuplicatesRequest request, CancellationToken cancellationToken)
    {
        var registerDuplicateRequest = new ProcessRegisterDuplicatesRequest()
        {
            Duplicates = request.Duplicates,
            AutoSubmitMergeJobs = request.AutoSubmitMergeJobs
        };

        var registerDuplicatesResponse = (await mediator.TrySend(registerDuplicateRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (!registerDuplicatesResponse.Success)
        {
            return new RegisterDuplicatesResponse()
            {
                Success = false,
                FailureReason = registerDuplicatesResponse.FailureReason
            };
        }

        return new RegisterDuplicatesResponse()
        {
            Success = true
        };
    }
}
