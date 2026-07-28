using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;

using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.RecordSyncEventsAgainstDataHubEntities;

public class RecordSyncEventsAgainstDataHubEntitiesRequestHandler<TSyncEvent>(IMediator mediator) : IHandler<RecordSyncEventsAgainstDataHubEntitiesRequest<TSyncEvent>, NullResponse>
    where TSyncEvent : SyncEvent
{
    public async Task<NullResponse> HandleAsync(RecordSyncEventsAgainstDataHubEntitiesRequest<TSyncEvent> request, CancellationToken cancellationToken)
    {
        var syncEventsWithDataHubEntityIds = (request.SyncEvents ?? new List<TSyncEvent>())
            .Where(syncEvent => !string.IsNullOrWhiteSpace(syncEvent.DataHubEntityId))
            .ToList();

        if (!syncEventsWithDataHubEntityIds.Any())
        {
            return new NullResponse();
        }

        var groupedByEntityType = syncEventsWithDataHubEntityIds.GroupBy(g => new { g.DataHubEntityType, g.SourceEntityType });

        foreach (var entityTypeGroup in groupedByEntityType)
        {
            var dataHubEntityIds = entityTypeGroup.Select(s => s.DataHubEntityId).Distinct().ToList();

            var getDataHubEntitiesByIdResponse = (await mediator.TrySend(new GetDataHubEntitiesByIdRequest()
            {
                EntityType = entityTypeGroup.Key.DataHubEntityType,
                EntityIds = dataHubEntityIds
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!getDataHubEntitiesByIdResponse.Success || getDataHubEntitiesByIdResponse.Results == null)
            {
                continue;
            }

            var dataHubEntities = getDataHubEntitiesByIdResponse.Results;

            var updatedDataHubEntities = new List<JObject>();
            foreach (var syncEvent in syncEventsWithDataHubEntityIds)
            {
                var dataHubEntity = dataHubEntities.FirstOrDefault(f => f.DataHubEntityId() == syncEvent.DataHubEntityId);
                if (dataHubEntity != null)
                {
                    var alternateKeys = dataHubEntity.Value<JArray>(nameof(DataHubEntity.alternateKeys))!.ToObject<List<AlternateKey>>();

                    var lastEntry = entityTypeGroup.Last();

                    var dataSourceAlternateKey = alternateKeys!.FirstOrDefault(f => f.Key == $"{lastEntry.DataSource}.{lastEntry.SourceEntityType}".ToLower());
                    if (dataSourceAlternateKey != null)
                    {
                        if (typeof(TSyncEvent) == typeof(MergeFailure))
                        {
                            dataSourceAlternateKey.LastMerge = syncEvent.Timestamp;
                            dataSourceAlternateKey.MergeError = (syncEvent as MergeFailure)?.FailureType;
                        }

                        if (typeof(TSyncEvent) == typeof(MergeSuccess))
                        {
                            dataSourceAlternateKey.LastMerge = syncEvent.Timestamp;
                            dataSourceAlternateKey.MergeError = null;
                        }

                        if (typeof(TSyncEvent) == typeof(SyncFailure))
                        {
                            dataSourceAlternateKey.LastSync = syncEvent.Timestamp;
                            dataSourceAlternateKey.SyncError = (syncEvent as SyncFailure)?.FailureReason;
                        }

                        if (typeof(TSyncEvent) == typeof(SyncSuccess))
                        {
                            dataSourceAlternateKey.LastSync = syncEvent.Timestamp;
                            dataSourceAlternateKey.SyncError = null;
                        }

                        dataHubEntity[nameof(DataHubEntity.alternateKeys)]?.Replace(JToken.FromObject(alternateKeys));
                        updatedDataHubEntities.Add(dataHubEntity);
                    }
                }
            }

            if (updatedDataHubEntities.Any())
            {
                _ = (await mediator.SendAsync(new UpsertDataHubEntitiesCommand()
                {
                    Entities = updatedDataHubEntities
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
            }
        }

        return new NullResponse();
    }
}
