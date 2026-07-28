using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;

public class AddTrackedEntityChangeSetsRequestHandler(IIdService idService, IMediator mediator) : IHandler<AddTrackedEntityChangeSetsRequest, AddTrackedEntityChangeSetsResponse>
{
    public async Task<AddTrackedEntityChangeSetsResponse> HandleAsync(AddTrackedEntityChangeSetsRequest request, CancellationToken cancellationToken)
    {
        var addChangeTrackingEntriesCommand = new CreateCosmosDocumentsCommand<ChangeTrackingEntry>()
        {
            Documents = request.Requests.Select(s => new ChangeTrackingEntry()
            {
                id = idService.NewId<ChangeTrackingEntry>(),
                EntryType = ChangeTrackingEntryTypes.Update,
                EntityType = s.EntityType,
                EntityId = s.SourceEntityId,
                DataSource = s.DataSource,
                Timestamp = s.TimeStamp,
                Data = s.ChangeSet
            }).ToList()
           
        };

        var response = (await mediator.TrySend( addChangeTrackingEntriesCommand, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        return new AddTrackedEntityChangeSetsResponse()
        {
            Successes = response.Successes,
            Failures = response.Failures.Select(s => s).ToList()
        };
    }
}