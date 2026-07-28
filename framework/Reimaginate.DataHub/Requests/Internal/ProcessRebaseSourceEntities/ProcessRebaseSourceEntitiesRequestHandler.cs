using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities;

public class ProcessRebaseSourceEntitiesRequestHandler(IMediator mediator) : IHandler<ProcessRebaseSourceEntitiesRequest, ProcessRebaseSourceEntitiesResponse>
{
    public async Task<ProcessRebaseSourceEntitiesResponse> HandleAsync(ProcessRebaseSourceEntitiesRequest request, CancellationToken cancellationToken)
    {
        var entitiesToProcess = request.EntityIds.Where(w => w != null).Distinct().ToList();
        var results = new ConcurrentBag<RebaseEntityTrackingResult>();

        while (entitiesToProcess.Any())
        {
            var batch = entitiesToProcess.Take(500).ToList();

            var getTrackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest()
            {
                DataSource = request.DataSource,
                EntityType = request.EntityType,
                EntityIds = batch

            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var trackingEntriesByEntity = getTrackingEntriesResponse.TrackingEntries.GroupBy(g => new { g.DataSource, g.EntityType, g.EntityId }).ToList();

            var initRequests = new ConcurrentBag<InitTrackedEntityRequest>();
            var entriesToDelete = new ConcurrentBag<ChangeTrackingEntry>();

            await Parallel.ForEachAsync(batch, new ParallelOptions()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount

            }, (entityId, _) =>
            {
                var entityTrackingEntries = trackingEntriesByEntity.FirstOrDefault(f => f.Key.EntityId == entityId)?.ToList() ?? new List<ChangeTrackingEntry>();

                if (!entityTrackingEntries.Any())
                {
                    results.Add(new RebaseEntityTrackingResult()
                    {
                        DataSource = request.DataSource,
                        EntityType = request.EntityType,
                        EntityId = entityId,
                        Success = false,
                        FailureReason = "NOT_FOUND"
                    });

                    return ValueTask.CompletedTask;
                }

                if (entityTrackingEntries.Any() && entityTrackingEntries.All(a => a.EntryType != "Init"))
                {
                    foreach (var entityTrackingEntry in entityTrackingEntries)
                    {
                        entriesToDelete.Add(entityTrackingEntry);
                    }
                    return ValueTask.CompletedTask;
                }

                var materializedEntity = ChangeTrackingHelper.ReassembleEntity(entityTrackingEntries, true);

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
                    return ValueTask.CompletedTask;
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
                        DataSource = request.DataSource,
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
                    DataSource = DataSources.DataHub,
                    EntityType = request.EntityType,
                    EntityId = entityId,
                    Success = true,
                    ArchivedTrackingEntries = archiveEntries.OrderBy(o => o.Timestamp).ToList()
                });
                return ValueTask.CompletedTask;
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



        return new ProcessRebaseSourceEntitiesResponse()
        {
            Results = results.ToList()
        };
    }
}

