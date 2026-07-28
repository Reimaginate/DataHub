using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKeys;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKey;

public class ProcessRegisterAlternateKeyRequestHandler(IMediator mediator) : IHandler<ProcessRegisterAlternateKeyRequest, ProcessRegisterAlternateKeyResponse>
{
    public async Task<ProcessRegisterAlternateKeyResponse> HandleAsync(ProcessRegisterAlternateKeyRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new ProcessRegisterAlternateKeysRequest()
        {
            Requests = [request]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return response.Responses.FirstOrDefault();
    }
}
