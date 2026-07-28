using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKey;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterAlternateKey;

public class RegisterAlternateKeyRequestHandler(IMediator mediator)
    : IHandler<RegisterAlternateKeyRequest, RegisterAlternateKeyResponse>
{
    public async Task<RegisterAlternateKeyResponse> HandleAsync(RegisterAlternateKeyRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new ProcessRegisterAlternateKeyRequest()
        {
            CorrelationId = request.CorrelationId,
            DataHubEntityId = request.DataHubEntityId,
            EntityType = request.EntityType,
            SourceEntityId = request.SourceEntityId,
            Key = request.Key,
            Untracked = request.Untracked,
            Replace = request.Replace,
            ReplaceSameDataSource = request.ReplaceSameDataSource,
            Silent = request.Silent
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new RegisterAlternateKeyResponse()
        {
            Success = response.Success,
            FailureReason = response.Exception
        };
    }
}
