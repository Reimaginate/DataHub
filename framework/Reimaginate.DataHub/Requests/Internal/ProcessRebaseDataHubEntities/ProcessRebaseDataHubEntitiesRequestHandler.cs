using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;

public class ProcessRebaseDataHubEntitiesRequestHandler(IMediator mediator, IProcessingLockService processingLockService) : IHandler<ProcessRebaseDataHubEntitiesRequest, ProcessRebaseDataHubEntitiesResponse>
{
    public async Task<ProcessRebaseDataHubEntitiesResponse> HandleAsync(ProcessRebaseDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {
        var entitiesToProcess = request.EntityIds.Where(w => w != null).Distinct().ToList();
        var results = new ConcurrentBag<RebaseEntityTrackingResult>();
        var lockOwner = Guid.NewGuid().ToString("N");

        while (entitiesToProcess.Any())
        {
            var batch = entitiesToProcess.Take(500).ToList();

            var lockFailure = await RebaseBatchLock.ExecuteAsync(processingLockService,
                batch.Select(entityId => $"entities/{DataSources.DataHub}/{request.EntityType}/{entityId}").ToList(), lockOwner,
                async (batchCancellationToken, ensureLease) =>
                {
                    ensureLease();
                    var getTrackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest()
                    {
                        DataSource = DataSources.DataHub,
                        EntityType = request.EntityType,
                        EntityIds = batch,
                        ObserveCancellation = true

                    }, batchCancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    var trackingEntriesByEntity = getTrackingEntriesResponse.TrackingEntries.GroupBy(g => new { g.DataSource, g.EntityType, g.EntityId }).ToList();

                    var getDataHubEntitiesResponse = (await mediator.TrySend(new GetMaterializedEntitiesByIdRequest
                    {
                        EntityType = request.EntityType,
                        EntityIds = batch
                    }, batchCancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    var materializedEntitiesDic = getDataHubEntitiesResponse.Results.ToDictionary(k => k.Value<string>(nameof(DataHubEntity.id)), v => v);

                    var plans = new ConcurrentBag<RebaseTrackingPlan>();

                    await Parallel.ForEachAsync(batch, new ParallelOptions()
                    {
                        CancellationToken = batchCancellationToken,
                        MaxDegreeOfParallelism = Environment.ProcessorCount

                    }, async (entityId, ct) =>
                    {
                        var entityTrackingEntries = trackingEntriesByEntity.FirstOrDefault(f => f.Key.EntityId == entityId)?.ToList() ?? new List<ChangeTrackingEntry>();

                        var rebaseTo = request.RebaseTo != null
                            ? entityTrackingEntries.Where(w => w.Timestamp <= request.RebaseTo).MaxBy(o => o.Timestamp)?.Timestamp
                            : entityTrackingEntries.MaxBy(o => o.Timestamp)?.Timestamp;

                        var archiveEntries = entityTrackingEntries.Where(w => w.Timestamp <= rebaseTo).ToList();
                        // An explicit cutoff before all existing history is a successful no-op.
                        if (entityTrackingEntries.Any() && !archiveEntries.Any())
                        {
                            results.Add(new RebaseEntityTrackingResult()
                            {
                                DataSource = DataSources.DataHub,
                                EntityType = request.EntityType,
                                EntityId = entityId,
                                Success = true,
                                ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                            });
                            return;
                        }

                        var currentInitEntry = archiveEntries.OrderBy(o => o.Timestamp).LastOrDefault(w => w.EntryType == ChangeTrackingEntryTypes.Init);
                        var conflictingBaselineAt = RebaseTrackingBaselines.FirstConflict(archiveEntries);
                        var hasLaterEntries = entityTrackingEntries.Any(entry => entry.Timestamp > rebaseTo);
                        JObject materializedEntity = null;
                        var useStoredEntityRecovery = false;

                        if (currentInitEntry != null && conflictingBaselineAt == null)
                        {
                            try
                            {
                                var materializeEntityResponse = (await mediator.TrySend(new MaterializeDataHubEntityRequest()
                                {
                                    EntityType = request.EntityType,
                                    EntityId = entityId,
                                    // Reconstruct from the latest baseline through the cutoff only.
                                    TrackingEntries = archiveEntries.Where(entry => entry == currentInitEntry ||
                                        (entry.EntryType != ChangeTrackingEntryTypes.Init && entry.Timestamp >= currentInitEntry.Timestamp)).ToList(),
                                    SkipSave = true,
                                    PreserveNullValues = true
                                }, ct)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                                materializedEntity = materializeEntityResponse.ResultingEntity;
                            }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                            catch (Exception ex)
                            {
                                if (hasLaterEntries || !ex.Message.StartsWith("Could not reassemble entity from change tracking"))
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
                            // The current stored entity cannot stand in for an earlier point in time.
                            if (hasLaterEntries)
                            {
                                results.Add(new RebaseEntityTrackingResult()
                                {
                                    EntityType = request.EntityType,
                                    EntityId = entityId,
                                    Success = false,
                                    FailureReason = "Failed to materialize tracked entity at the rebase cutoff; original tracking history was preserved.",
                                    ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                                });
                                return;
                            }

                            if (!materializedEntitiesDic.TryGetValue(entityId, out var value))
                            {
                                results.Add(new RebaseEntityTrackingResult()
                                {
                                    EntityType = request.EntityType,
                                    EntityId = entityId,
                                    Success = false,
                                    FailureReason = "NOT_FOUND",
                                    ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                                });

                                return;
                            }

                            // Stored lastUpdated is processing time, not the history's source-event time.
                            // It limits historical recovery, but does not prove which changes were saved.
                            if (request.RebaseTo != null &&
                                (!value[nameof(DataHubEntity.lastUpdated)].TryAsDateTimeOffset(out var storedTimestamp) || storedTimestamp > request.RebaseTo.Value))
                            {
                                results.Add(new RebaseEntityTrackingResult()
                                {
                                    EntityType = request.EntityType,
                                    EntityId = entityId,
                                    Success = false,
                                    FailureReason = "Failed to materialize tracked entity at the rebase cutoff; the stored entity cannot be confirmed to represent that point in time.",
                                    ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                                });
                                return;
                            }

                            materializedEntity = value;
                            useStoredEntityRecovery = true;
                        }

                        if (rebaseTo == null)
                        {
                            if (!materializedEntity[nameof(DataHubEntity.lastUpdated)].TryAsDateTimeOffset(out var storedTimestamp))
                            {
                                results.Add(new RebaseEntityTrackingResult
                                {
                                    DataSource = DataSources.DataHub,
                                    EntityType = request.EntityType,
                                    EntityId = entityId,
                                    Success = false,
                                    FailureReason = "Stored-entity recovery without tracking history requires a valid lastUpdated timestamp; no baseline was written.",
                                    ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                                });
                                return;
                            }
                            rebaseTo = storedTimestamp;
                        }

                        InitTrackedEntityRequest newInitEntry = null;
                        if (!useStoredEntityRecovery && currentInitEntry != null && currentInitEntry.Timestamp == rebaseTo &&
                            !archiveEntries.Any(entry => entry.EntryType == ChangeTrackingEntryTypes.Update && entry.Timestamp >= currentInitEntry.Timestamp))
                        {
                            archiveEntries.Remove(currentInitEntry);
                        }
                        else
                        {
                            newInitEntry = new InitTrackedEntityRequest()
                            {
                                DataSource = DataSources.DataHub,
                                EntityType = request.EntityType,
                                EntityData = RebaseTrackingPersistence.CreateBaselineData(materializedEntity,
                                    conflictingBaselineAt.HasValue ? RebaseTrackingBaselines.CreationDateBaseline(archiveEntries, currentInitEntry) : currentInitEntry),
                                EntityId = entityId,
                                Timestamp = rebaseTo
                            };
                        }

                        plans.Add(new RebaseTrackingPlan(newInitEntry, archiveEntries, new RebaseEntityTrackingResult()
                        {
                            EntityType = request.EntityType,
                            EntityId = entityId,
                            ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                        }, RetainedBaseline: newInitEntry == null ? currentInitEntry : null,
                            UseStoredEntityRecovery: useStoredEntityRecovery,
                            IncludeArchivedTrackingEntries: request.IncludeArchivedTrackingEntries));
                    });

                    var batchPlans = plans.ToList();
                    await RebaseTrackingPersistence.PersistAsync(mediator, batchPlans, batchCancellationToken, ensureLease);
                    foreach (var plan in batchPlans) results.Add(plan.Result);
                }, cancellationToken);

            if (lockFailure != null)
            {
                foreach (var entityId in batch)
                    results.Add(new RebaseEntityTrackingResult
                    {
                        DataSource = DataSources.DataHub,
                        EntityType = request.EntityType,
                        EntityId = entityId,
                        Success = false,
                        FailureReason = lockFailure,
                        ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                    });
            }

            entitiesToProcess.RemoveRange(0, batch.Count);
        }
        
        return new ProcessRebaseDataHubEntitiesResponse()
        {
            Results = results.ToList()
        };
    }
}

