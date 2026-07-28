using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;

public class InitTrackedEntitiesRequestHandler(IIdService idService, IMediator mediator, ITimeService timeService)
    : IHandler<InitTrackedEntitiesRequest, InitTrackedEntitiesResponse>
{
    public async Task<InitTrackedEntitiesResponse> HandleAsync(InitTrackedEntitiesRequest request, CancellationToken cancellationToken)
    {
        var addChangeTrackingEntriesCommand = new CreateCosmosDocumentsCommand<ChangeTrackingEntry>()
        {
            Documents = request.Requests.Select(s => new ChangeTrackingEntry()
            {
                id = idService.NewId<ChangeTrackingEntry>(),
                EntryType = ChangeTrackingEntryTypes.Init,
                EntityType = s.EntityType,
                EntityId = s.EntityId,
                DataSource = s.DataSource,
                Timestamp = s.Timestamp ?? timeService.Now(),
                Data = s.EntityData
            }).ToList()

        };

        var response = (await mediator.TrySend( addChangeTrackingEntriesCommand, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        return new InitTrackedEntitiesResponse()
        {
            Successes = response.Successes,
            Failures = response.Failures.ToList()
        };
    }
}