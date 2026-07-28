using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.CreateChangeTrackingEntry;

public class CreateChangeTrackingEntryRequestHandler(IMediator mediator, IIdService idService, ITimeService timeService)
    : IHandler<CreateChangeTrackingEntryRequest, ChangeTrackingEntry>
{
    public async Task<ChangeTrackingEntry> HandleAsync(CreateChangeTrackingEntryRequest request, CancellationToken cancellationToken)
    {
        var changeTrackingEntry = new ChangeTrackingEntry()
        {
            id = idService.NewId<ChangeTrackingEntry>(),
            EntryType = request.EntryType,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            DataSource = request.DataSource,
            Timestamp = request.Timestamp ?? timeService.Now(),
            Data = request.Data
        };

        if (request.SaveNow)
        {
            var createChangeTrackingCommand = new CreateCosmosDocumentsCommand<ChangeTrackingEntry>()
            {
                Documents = new List<ChangeTrackingEntry>() { changeTrackingEntry }
            };

            var response = (await mediator.TrySend(createChangeTrackingCommand, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            if (response.Failures.Any()) throw new AggregateException(response.Failures.Select(s => s.Error));
        }

        return changeTrackingEntry;
    }
}