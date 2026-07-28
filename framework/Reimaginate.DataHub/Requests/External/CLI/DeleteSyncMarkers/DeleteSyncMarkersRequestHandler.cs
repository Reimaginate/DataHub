using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Reimaginate.DataHub;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteSyncMarkers;

public class DeleteSyncMarkersRequestHandler(IMediator mediator) : IHandler<DeleteSyncMarkersRequest, DeleteSyncMarkersResponse>
{
    public async Task<DeleteSyncMarkersResponse> HandleAsync(DeleteSyncMarkersRequest request, CancellationToken cancellationToken)
    {
        var whereClauses = new List<string>();
        var parameters = new List<DataHubQueryParameter>();
        if (!string.IsNullOrWhiteSpace(request.AgentId))
        {
            whereClauses.Add("x.AgentId = @agentId");
            parameters.Add(new DataHubQueryParameter { Name = "agentId", Value = request.AgentId });
        }

        if (!string.IsNullOrWhiteSpace(request.DataSource))
        {
            whereClauses.Add("x.DataSource = @dataSource");
            parameters.Add(new DataHubQueryParameter { Name = "dataSource", Value = request.DataSource });
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            whereClauses.Add("x.EntityType = @entityType");
            parameters.Add(new DataHubQueryParameter { Name = "entityType", Value = request.EntityType });
        }

        var getSyncMarkersResponse = (await mediator.TrySend(new GetSyncMarkersRequest
        {
            User = request.User,
            WhereClause = whereClauses.Any() ? string.Join(" and ", whereClauses) : null,
            Parameters = parameters
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var response = (await mediator.TrySend( new DeleteCosmosDocumentsCommand<SyncMarker>()
        {
            Documents = getSyncMarkersResponse.Results
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new DeleteSyncMarkersResponse()
        {
            Success = !response.Failures.Any(),
            Exceptions = response.Failures.Select(failure => failure.Error).ToList()
        };
    }
}
