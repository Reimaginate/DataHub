using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKey;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKeys;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;


namespace Reimaginate.DataHub.Requests.External.CLI.RegisterAlternateKeys;

public class RegisterAlternateKeysRequestHandler(IMediator mediator, ITimeService timeService) : IHandler<RegisterAlternateKeysRequest, RegisterAlternateKeysResponse>
{
    private readonly ITimeService _timeService = timeService;

    public async Task<RegisterAlternateKeysResponse> HandleAsync(RegisterAlternateKeysRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new ProcessRegisterAlternateKeysRequest()
        {
            CorrelationId = request.CorrelationId,
            Requests = request.Requests.Select(req => new ProcessRegisterAlternateKeyRequest()
            {
                CorrelationId = request.CorrelationId,
                DataHubEntityId = req.DataHubEntityId,
                EntityType = req.EntityType,
                SourceEntityId = req.SourceEntityId,
                Key = req.Key,
                Untracked = req.Untracked,
                Replace = req.Replace,
                ReplaceSameDataSource = req.ReplaceSameDataSource,
                Silent = request.Silent
            }).ToList()
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new RegisterAlternateKeysResponse()
        {
            Responses = response.Responses.Select(s => new RegisterAlternateKeyResponse()
            {
                FailureReason = s.Exception,
                Success = s.Success
            }).ToList()
        };
    }
}
