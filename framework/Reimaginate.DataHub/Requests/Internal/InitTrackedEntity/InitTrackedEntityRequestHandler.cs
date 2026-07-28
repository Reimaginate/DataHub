using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;

public class InitTrackedEntityRequestHandler(IIdService idService, IMediator mediator, ITimeService timeService)
    : IHandler<InitTrackedEntityRequest, ChangeTrackingEntry>
{
    public async Task<ChangeTrackingEntry> HandleAsync(InitTrackedEntityRequest request, CancellationToken cancellationToken)
    {
        var createChangeTrackingCommand = new CreateCosmosDocumentsCommand<ChangeTrackingEntry>()
        {
            Documents = new List<ChangeTrackingEntry>(){
                new() {
                    id = idService.NewId<ChangeTrackingEntry>(),
                    EntryType = ChangeTrackingEntryTypes.Init,
                    EntityType = request.EntityType,
                    EntityId = request.EntityId,
                    DataSource = request.DataSource,
                    Timestamp = request.Timestamp ?? request.EntityData[nameof(DataHubEntity.lastUpdated)]?.AsDateTimeOffset() ?? timeService.Now(),
                    Data = request.EntityData
                }}
        };

        var response = (await mediator.TrySend( createChangeTrackingCommand, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (response.Failures.Any()) throw new AggregateException(response.Failures.Select(s => s.Error));
        return response.Successes.FirstOrDefault();
    }
}
