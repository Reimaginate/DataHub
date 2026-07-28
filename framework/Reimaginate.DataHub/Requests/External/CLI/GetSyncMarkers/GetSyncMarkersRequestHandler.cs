using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetSyncMarkers;

public class GetSyncMarkersRequestHandler(IMediator mediator) : IHandler<GetSyncMarkersRequest, GetSyncMarkersResponse>
{
    public async Task<GetSyncMarkersResponse> HandleAsync(GetSyncMarkersRequest request, CancellationToken cancellationToken)
    {
        var results = new List<SyncMarker>();

        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<SyncMarker>()
        {
            WhereClause = request.WhereClause,
            ContinuationToken = request.ContinuationToken,
            OrderBy = request.OrderBy,
            GetTotalResultCount = false,
            PageSize = request.PageSize,
            Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        results.AddRange(response.Results);

        while (response.MoreResultsAvailable)
        {
            response = (await mediator.TrySend(new GetCosmosDocumentsQuery<SyncMarker>()
            {
                WhereClause = request.WhereClause,
                ContinuationToken = response.ContinuationToken,
                OrderBy = request.OrderBy,
                GetTotalResultCount = false,
                PageSize = request.PageSize,
                Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            results.AddRange(response.Results);
        }

        return new GetSyncMarkersResponse()
        {
            Results = results
        };
    }
}
