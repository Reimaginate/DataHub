using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Diagnostics;

public interface IDataHubTraceQueryService
{
    Task<GetTraceResponse> QueryTraceAsync(GetTraceRequest request, CancellationToken cancellationToken);
}
