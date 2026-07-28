using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DeleteTrackingData;

public class DeleteTrackingDataRequestHandler(IMediator mediator) : IHandler<DeleteTrackingDataRequest, DeleteTrackingDataResponse>
{
    public async Task<DeleteTrackingDataResponse> HandleAsync(DeleteTrackingDataRequest request, CancellationToken cancellationToken)
    {
        var getTrackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest()
        {
            DataSource = request.DataSource,
            EntityType = request.EntityType,
            EntityIds = request.EntityIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var deleteResponse = (await mediator.TrySend(new DeleteCosmosDocumentsCommand<ChangeTrackingEntry>()
        {
            Documents = getTrackingEntriesResponse.TrackingEntries
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        
        return new DeleteTrackingDataResponse()
        {
            Success = !deleteResponse.Failures.Any(),
            Failures = deleteResponse.Failures.Select(s => new DeleteTrackingDataFailure()
            {
                DataSource = request.DataSource,
                EntityType = request.EntityType,
                EntityId = s.Item.EntityId,
                Exception = s.Error
            }).ToList()
        };
    }
}