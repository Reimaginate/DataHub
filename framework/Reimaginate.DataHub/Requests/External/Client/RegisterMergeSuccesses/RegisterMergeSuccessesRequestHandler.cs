using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterMergeSuccesses;

public class RegisterMergeSuccessesRequestHandler(IMediator mediator) : IHandler<RegisterMergeSuccessesRequest, NullResponse>
{
    public async Task<NullResponse> HandleAsync(RegisterMergeSuccessesRequest request, CancellationToken cancellationToken)
    {
        _ = (await mediator.SendAsync(new LogEventsRequest<MergeSuccess>
        {
            Events = request.MergeSuccesses
        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

        return new NullResponse();
    }
}
