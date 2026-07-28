using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKey;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKeys;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;


namespace Reimaginate.DataHub.Requests.External.Client.RegisterAlternateKeys;

public class RegisterAlternateKeysRequestHandler(IMediator mediator) : IHandler<RegisterAlternateKeysRequest, RegisterAlternateKeysResponse>
{
    public async Task<RegisterAlternateKeysResponse> HandleAsync(RegisterAlternateKeysRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new ProcessRegisterAlternateKeysRequest()
        {
            CorrelationId = request.CorrelationId,
            Requests = request.Requests.Select(s => new ProcessRegisterAlternateKeyRequest()
            {
                CorrelationId = request.CorrelationId,
                DataHubEntityId = s.DataHubEntityId,
                EntityType = s.EntityType,
                SourceEntityId = s.SourceEntityId,
                Key = s.Key,
                Untracked = s.Untracked,
                Silent = request.Silent
            }).ToList()
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new RegisterAlternateKeysResponse()
        {
            Success = response.Responses.Any(a=>!a.Success),
            Responses = response.Responses.Select(s => new RegisterAlternateKeyResponse()
            {
                FailureReason = s.Exception,
                Success = s.Success
            }).ToList()
        };
    }
}
