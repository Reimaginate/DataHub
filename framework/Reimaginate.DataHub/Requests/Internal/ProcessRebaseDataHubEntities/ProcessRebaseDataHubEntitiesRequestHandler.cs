using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;

public class ProcessRebaseDataHubEntitiesRequestHandler(IMediator mediator) : IHandler<ProcessRebaseDataHubEntitiesRequest, ProcessRebaseDataHubEntitiesResponse>
{
    public async Task<ProcessRebaseDataHubEntitiesResponse> HandleAsync(ProcessRebaseDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {
        var entitiesToProcess = request.EntityIds.Where(w => w != null).Distinct().ToList();
        var results = new ConcurrentBag<RebaseEntityTrackingResult>();

        while (entitiesToProcess.Any())
        {
            var batch = entitiesToProcess.Take(500).ToList();

            var getTrackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = request.EntityType,
                EntityIds = batch

            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var trackingEntriesByEntity = getTrackingEntriesResponse.TrackingEntries.GroupBy(g => new { g.DataSource, g.EntityType, g.EntityId }).ToList();

            var getDataHubEntitiesResponse = (await mediator.TrySend(new GetMaterializedEntitiesByIdRequest
            {
                EntityType = request.EntityType,
                EntityIds = batch
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var materializedEntitiesDic = getDataHubEntitiesResponse.Results.ToDictionary(k => k.Value<string>(nameof(DataHubEntity.id)), v => v);

            var initRequests = new ConcurrentBag<InitTrackedEntityRequest>();
            var entriesToDelete = new ConcurrentBag<ChangeTrackingEntry>();

            await Parallel.ForEachAsync(batch, new ParallelOptions()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount

            }, async (entityId, ct) =>
            {
                var entityTrackingEntries = trackingEntriesByEntity.FirstOrDefault(f => f.Key.EntityId == entityId)?.ToList() ?? new List<ChangeTrackingEntry>();

                JObject materializedEntity = null;

                if (entityTrackingEntries.Any(a => a.EntryType == "Init"))
                {
                    try
                    {
                        var materializeEntityResponse = (await mediator.TrySend(new MaterializeDataHubEntityRequest()
                        {
                            EntityType = request.EntityType,
                            EntityId = entityId,
                            TrackingEntries = entityTrackingEntries,
                            SkipSave = true
                        }, ct)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                        materializedEntity = materializeEntityResponse.ResultingEntity;
                    }
                    catch (Exception ex)
                    {
                        if (!ex.Message.StartsWith("Could not reassemble entity from change tracking"))
                        {
                            results.Add(new RebaseEntityTrackingResult()
                            {
                                EntityType = request.EntityType,
                                EntityId = entityId,
                                Success = false,
                                FailureReason = $"Failed to materialize tracked entity: {ex.Message}"
                            });
                            return;
                        }
                    }
                }

                if (materializedEntity == null)
                {
                    if (!materializedEntitiesDic.TryGetValue(entityId, out var value))
                    {
                        results.Add(new RebaseEntityTrackingResult()
                        {
                            EntityType = request.EntityType,
                            EntityId = entityId,
                            Success = false,
                            FailureReason = "NOT_FOUND"
                        });

                        return;
                    }

                    materializedEntity = value;
                }

                var rebaseTo = request.RebaseTo != null
                    ? entityTrackingEntries.Where(w => w.Timestamp <= request.RebaseTo).MaxBy(o => o.Timestamp)?.Timestamp
                    : null;

                rebaseTo ??= entityTrackingEntries.MaxBy(o => o.Timestamp)?.Timestamp ?? materializedEntity.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated));

                var archiveEntries = entityTrackingEntries.Where(w => w.Timestamp <= rebaseTo).ToList();

                if (!archiveEntries.Any())
                {
                    results.Add(new RebaseEntityTrackingResult()
                    {
                        EntityType = request.EntityType,
                        EntityId = entityId,
                        Success = true
                    });
                    return;
                }

                var currentInitEntry = entityTrackingEntries.OrderBy(o => o.Timestamp).LastOrDefault(w => w.EntryType == "Init");
                if (currentInitEntry != null && currentInitEntry.Timestamp == request.RebaseTo)
                {
                    archiveEntries.Remove(currentInitEntry);
                }
                else
                {
                    var newInitEntry = new InitTrackedEntityRequest()
                    {
                        DataSource = DataSources.DataHub,
                        EntityType = request.EntityType,
                        EntityData = ChangeTrackingHelper.StripBaseProperties(materializedEntity).RemoveNullValues(),
                        EntityId = entityId,
                        Timestamp = rebaseTo
                    };
                    initRequests.Add(newInitEntry);
                }

                foreach (var entry in archiveEntries)
                {
                    entriesToDelete.Add(entry);
                }

                results.Add(new RebaseEntityTrackingResult()
                {
                    EntityType = request.EntityType,
                    EntityId = entityId,
                    Success = true,
                    ArchivedTrackingEntries = archiveEntries.OrderBy(o => o.Timestamp).ToList()
                });
            });

            if (initRequests.Any())
            {
                var initTrackedEntitiesResponse = (await mediator.TrySend(new InitTrackedEntitiesRequest()
                {
                    Requests = initRequests.ToList()
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                //TODO: Handle Failures
            }

            if (entriesToDelete.Any())
            {
                var deleteResponse = (await mediator.TrySend(new DeleteCosmosDocumentsCommand<ChangeTrackingEntry>()
                {
                    Documents = entriesToDelete.ToList()
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                //TODO: Handle Failures
            }

            entitiesToProcess.RemoveRange(0, batch.Count);
        }
        
        return new ProcessRebaseDataHubEntitiesResponse()
        {
            Results = results.ToList()
        };
    }
}

