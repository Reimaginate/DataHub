using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;

public class AddTrackedEntityChangeSetRequestHandler(IIdService idService, IMediator mediator) : IHandler<AddTrackedEntityChangeSetRequest, ChangeTrackingEntry>
{
    public async Task<ChangeTrackingEntry> HandleAsync(AddTrackedEntityChangeSetRequest request, CancellationToken cancellationToken)
    {
        var createChangeTrackingCommand = new UpsertCosmosDocumentsCommand<ChangeTrackingEntry>()
        {
            Documents = new List<ChangeTrackingEntry>(){
                new(){
                    id = idService.NewId<ChangeTrackingEntry>(),
                    EntryType = ChangeTrackingEntryTypes.Update,
                    EntityType = request.EntityType,
                    EntityId = request.SourceEntityId,
                    DataSource = request.DataSource,
                    Timestamp = request.TimeStamp,
                    Data = ChangeTrackingHelper.StripBaseProperties(JObject.FromObject(request.ChangeSet))
                }
            }
        };

        var response = (await mediator.TrySend( createChangeTrackingCommand, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (response.Failures.Any()) throw new AggregateException(response.Failures.Select(s => s.Error));
        return response.Successes.FirstOrDefault();
    }
}