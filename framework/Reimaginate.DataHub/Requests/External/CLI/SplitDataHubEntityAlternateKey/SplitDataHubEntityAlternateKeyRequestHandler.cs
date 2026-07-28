using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessSplitDataHubEntityAlternateKey;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.SplitDataHubEntityAlternateKey;

public class SplitDataHubEntityAlternateKeyRequestHandler(IMediator mediator)
    : IHandler<SplitDataHubEntityAlternateKeyRequest, SplitDataHubEntityAlternateKeyResponse>
{
    public async Task<SplitDataHubEntityAlternateKeyResponse> HandleAsync(SplitDataHubEntityAlternateKeyRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new ProcessSplitDataHubEntityAlternateKeyRequest
        {
            CorrelationId = request.CorrelationId,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Key = request.Key,
            Value = request.Value,
            Silent = request.Silent,
            DryRun = request.DryRun
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return response;
    }
}
