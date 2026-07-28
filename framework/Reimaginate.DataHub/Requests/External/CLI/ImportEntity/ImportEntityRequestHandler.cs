using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessImportEntity;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.ImportEntity;

public class ImportEntityRequestHandler(IMediator mediator) : IHandler<ImportEntityRequest, ImportEntityResponse>
{
    public async Task<ImportEntityResponse> HandleAsync(ImportEntityRequest request, CancellationToken cancellationToken)
    {
        var processImportEntityRequest = new ProcessImportEntityRequest()
        {
            CorrelationId = null,
            Data = request.Data,
            EntityId = request.EntityId,
            EntityType = request.EntityType,
            OverwriteIfExists = request.OverwriteIfExists,
            Untracked = request.Untracked
        };

        var response = (await mediator.TrySend(processImportEntityRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        return response;
    }
}