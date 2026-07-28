using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesById;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DeleteTrackingDataEntries;

public class DeleteTrackingDataEntriesRequestHandler(IMediator mediator) : IHandler<DeleteTrackingDataEntriesRequest, DeleteTrackingDataResponse>
{
    public async Task<DeleteTrackingDataResponse> HandleAsync(DeleteTrackingDataEntriesRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<DataAccessFailure<ChangeTrackingEntry>>();
        
        var getTrackingEntriesResponse = (await mediator.TrySend(new GetTrackingEntriesByIdQuery()
        {
            Ids = request.Ids
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        while (getTrackingEntriesResponse.Results.Any())
        {
            var batchResponse = (await mediator.TrySend(new DeleteCosmosDocumentsCommand<ChangeTrackingEntry>()
            {
                Documents = getTrackingEntriesResponse.Results
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            failures.AddRange(batchResponse.Failures);

            if (!getTrackingEntriesResponse.MoreResultsAvailable || batchResponse.Successes?.Any() != true || batchResponse.Failures?.Any() == true)
            {
                break;
            }

            getTrackingEntriesResponse = (await mediator.TrySend(new GetTrackingEntriesByIdQuery()
            {
                Ids = request.Ids
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        }

        
        return new DeleteTrackingDataResponse()
        {
            Success = !failures.Any(),
            Failures = failures.Select(s => new DeleteTrackingDataEntryFailure()
            {
                Id = s.Item.EntityId,
                FailureReason = s.Error.Message
            }).ToList()
        };
    }
}
