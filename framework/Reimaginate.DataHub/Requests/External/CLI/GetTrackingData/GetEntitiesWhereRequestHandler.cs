using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetTrackingData;

public class GetTrackingDataRequestHandler(IMediator mediator) : IHandler<GetTrackingDataRequest, GetTrackingDataResponse>
{
    public async Task<GetTrackingDataResponse> HandleAsync(GetTrackingDataRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend( new GetTrackingEntriesQuery()
        {
            DataSource = request.DataSource,
            EntityType = request.EntityType,
            EntityIds = request.EntityIds,
            FromDateTime = request.FromDateTime,
            ToDateTime = request.ToDateTime,
            ContinuationToken = request.ContinuationToken,
            PageSize = request.PageSize,
            GetTotalResultCount = request.GetTotalResultCount
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return new GetTrackingDataResponse()
        {
            Results = response.Results,
            ContinuationToken = response.ContinuationToken,
            MoreResultsAvailable = response.MoreResultsAvailable,
            ResultCount = response.ResultCount
        };
    }
}