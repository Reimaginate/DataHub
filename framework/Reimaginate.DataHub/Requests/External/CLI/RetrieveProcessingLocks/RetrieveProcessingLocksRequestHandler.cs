using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;

namespace Reimaginate.DataHub.Requests.External.CLI.RetrieveProcessingLocks;

public class RetrieveProcessingLocksRequestHandler(IProcessingLockService processingLockService) : IHandler<RetrieveProcessingLocksRequest, RetrieveProcessingLocksResponse>
{
    public async Task<RetrieveProcessingLocksResponse> HandleAsync(RetrieveProcessingLocksRequest request, CancellationToken cancellationToken)
    {
        var getLocksResponse = await processingLockService.GetLocksAsync(cancellationToken);
        
        return new RetrieveProcessingLocksResponse()
        {
            ProcessingLocks = getLocksResponse.Result
        };
    }
}