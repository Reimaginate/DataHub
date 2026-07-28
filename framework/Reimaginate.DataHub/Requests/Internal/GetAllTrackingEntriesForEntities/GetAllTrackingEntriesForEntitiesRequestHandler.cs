using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;

public class GetAllTrackingEntriesForEntitiesRequestHandler(IMediator mediator) : IHandler<GetAllTrackingEntriesForEntitiesRequest, GetAllTrackingEntriesForEntitiesResponse>
{
    public async Task<GetAllTrackingEntriesForEntitiesResponse> HandleAsync(GetAllTrackingEntriesForEntitiesRequest request, CancellationToken cancellationToken)
    {
        var results = new List<ChangeTrackingEntry>();

        var entityIdsToProcess = new List<string>(request.EntityIds);

        while (entityIdsToProcess.Any())
        {
            var batch = entityIdsToProcess.Take(5000).ToList();

            var getTrackingEntriesResponse = (await mediator.TrySend(new GetTrackingEntriesQuery()
            {
                DataSource = request.DataSource,
                EntityType = request.EntityType,
                EntityIds = batch,
                PageSize = 5000
            }, CancellationToken.None)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            results.AddRange(getTrackingEntriesResponse.Results.Where(w => w != null));

            while (getTrackingEntriesResponse.MoreResultsAvailable)
            {
                getTrackingEntriesResponse = (await mediator.TrySend(new GetTrackingEntriesQuery()
                {
                    DataSource = request.DataSource,
                    EntityIds = batch,
                    EntityType = request.EntityType,
                    PageSize = 5000,
                    ContinuationToken = getTrackingEntriesResponse.ContinuationToken
                }, CancellationToken.None)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                results.AddRange(getTrackingEntriesResponse.Results.Where(w => w != null));
            }

            entityIdsToProcess.RemoveRange(0, batch.Count);
        }

        return new GetAllTrackingEntriesForEntitiesResponse()
        {
            TrackingEntries = results
        };
    }
}