using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessImportEntities;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.ImportEntities;

public class ImportEntitiesRequestHandler(IMediator mediator) : IHandler<ImportEntitiesRequest, List<ImportEntityResponse>>
{
    public async Task<List<ImportEntityResponse>> HandleAsync(ImportEntitiesRequest request, CancellationToken cancellationToken)
    {
        var results = (await mediator.TrySend(new ProcessImportEntitiesRequest()
        {
            CorrelationId = request.CorrelationId,
            ImportEntityRequests = request.ImportEntityRequests,
            Silent = request.Silent,
            DispatchNotifications = request.DispatchNotifications,
            OverwriteIfExists = request.OverwriteIfExists,
            Untracked = request.Untracked
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return results;
    }
}