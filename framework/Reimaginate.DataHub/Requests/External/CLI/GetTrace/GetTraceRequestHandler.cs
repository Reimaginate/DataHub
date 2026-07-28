using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetTrace;

public class GetTraceRequestHandler(IDataHubTraceQueryService traceQueryService) : IHandler<GetTraceRequest, GetTraceResponse>
{
    public Task<GetTraceResponse> HandleAsync(GetTraceRequest request, CancellationToken cancellationToken)
        => traceQueryService.QueryTraceAsync(request, cancellationToken);
}
