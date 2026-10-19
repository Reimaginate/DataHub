using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries.GetSourceRebaseIdentities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities;

public class ProcessRebaseSourceEntitiesRequestHandler(IMediator mediator, IProcessingLockService processingLockService) : IHandler<ProcessRebaseSourceEntitiesRequest, ProcessRebaseSourceEntitiesResponse>
{
    public async Task<ProcessRebaseSourceEntitiesResponse> HandleAsync(ProcessRebaseSourceEntitiesRequest request, CancellationToken cancellationToken)
    {
        var entitiesToProcess = request.EntityIds.Where(w => w != null).Distinct().ToList();
        var results = new ConcurrentBag<RebaseEntityTrackingResult>();
        var lockOwner = Guid.NewGuid().ToString("N");

        while (entitiesToProcess.Any())
        {
            var batch = entitiesToProcess.Take(500).ToList();
            cancellationToken.ThrowIfCancellationRequested();
            var storedIdentities = (await mediator.TrySend(new GetSourceRebaseIdentitiesQuery
            {
                DataSource = request.DataSource, EntityType = request.EntityType, EntityIds = batch
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var identities } => identities };
            cancellationToken.ThrowIfCancellationRequested();
            // Keep the normal locked NOT_FOUND path when there is no stored identity.
            if (storedIdentities.Count == 0) storedIdentities = [new(request.DataSource, request.EntityType)];

            var lockFailure = await RebaseBatchLock.ExecuteAsync(processingLockService,
                storedIdentities.SelectMany(identity => batch.Select(entityId => $"entities/{identity.DataSource}/{identity.EntityType}/{entityId}")).ToList(), lockOwner,
                async (batchCancellationToken, ensureLease) =>
                {
                    var trackingEntries = new List<ChangeTrackingEntry>();
                    foreach (var storedIdentity in storedIdentities)
                    {
                        ensureLease();
                        var getTrackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest()
                        {
                            DataSource = storedIdentity.DataSource,
                            EntityType = storedIdentity.EntityType,
                            EntityIds = batch,
                            ObserveCancellation = true

                        }, batchCancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                        trackingEntries.AddRange(getTrackingEntriesResponse.TrackingEntries);
                    }

                    var trackingEntriesByEntity = trackingEntries.GroupBy(g => new { g.DataSource, g.EntityType, g.EntityId }).ToLookup(g => g.Key.EntityId);

                    var plans = new ConcurrentBag<RebaseTrackingPlan>();

                    await Parallel.ForEachAsync(batch, new ParallelOptions()
                    {
                        CancellationToken = batchCancellationToken,
                        MaxDegreeOfParallelism = Environment.ProcessorCount

                    }, (entityId, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        var histories = trackingEntriesByEntity[entityId].ToList();
                        if (histories.Count > 1)
                        {
                            results.Add(new RebaseEntityTrackingResult
                            {
                                DataSource = request.DataSource,
                                EntityType = request.EntityType,
                                EntityId = entityId,
                                Success = false,
                                FailureReason = histories.Select(history => history.Key.DataSource).Distinct(StringComparer.Ordinal).Count() == 1
                                    ? "AMBIGUOUS_SOURCE_ENTITY_TYPE: Multiple stored source entity types match this entity; original tracking histories were preserved."
                                    : "AMBIGUOUS_SOURCE_IDENTITY: Multiple stored data source and entity type identities match this entity; original tracking histories were preserved."
                            });
                            return ValueTask.CompletedTask;
                        }
                        var history = histories.SingleOrDefault();
                        var identity = history != null ? new SourceRebaseIdentity(history.Key.DataSource, history.Key.EntityType)
                            : storedIdentities.Count == 1 ? storedIdentities[0] : new(request.DataSource, request.EntityType);
                        var dataSource = identity.DataSource;
                        var entityType = identity.EntityType;
                        var entityTrackingEntries = history?.ToList() ?? new List<ChangeTrackingEntry>();

                        if (!entityTrackingEntries.Any())
                        {
                            results.Add(new RebaseEntityTrackingResult()
                            {
                                DataSource = dataSource,
                                EntityType = entityType,
                                EntityId = entityId,
                                Success = false,
                                FailureReason = "NOT_FOUND"
                            });

                            return ValueTask.CompletedTask;
                        }

                        var rebaseTo = request.RebaseTo != null
                            ? entityTrackingEntries.Where(w => w.Timestamp <= request.RebaseTo).MaxBy(o => o.Timestamp)?.Timestamp
                            : entityTrackingEntries.MaxBy(o => o.Timestamp)?.Timestamp;

                        var archiveEntries = entityTrackingEntries.Where(w => w.Timestamp <= rebaseTo).ToList();

                        if (!archiveEntries.Any())
                        {
                            results.Add(new RebaseEntityTrackingResult()
                            {
                                DataSource = dataSource,
                                EntityType = entityType,
                                EntityId = entityId,
                                Success = true,
                                ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                            });
                            return ValueTask.CompletedTask;
                        }

                        var conflictingBaselineAt = RebaseTrackingBaselines.FirstConflict(archiveEntries);
                        string incompleteReason = null;
                        if (conflictingBaselineAt.HasValue)
                        {
                            incompleteReason = $"Partial rebase: stopped before conflicting baseline timestamp {conflictingBaselineAt.Value:O}. " +
                                "The conflicting timestamp group and all later entries were retained for separate recovery.";
                            archiveEntries = archiveEntries.Where(entry => entry.Timestamp < conflictingBaselineAt.Value).ToList();
                        }

                        var currentInitEntry = archiveEntries.OrderBy(o => o.Timestamp).LastOrDefault(w => w.EntryType == ChangeTrackingEntryTypes.Init);
                        if (currentInitEntry == null)
                        {
                            results.Add(new RebaseEntityTrackingResult()
                            {
                                DataSource = dataSource,
                                EntityType = entityType,
                                EntityId = entityId,
                                Success = false,
                                FailureReason = incompleteReason ?? "Could not find initial tracking entry at or before the rebase cutoff; original tracking history was preserved.",
                                ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                            });
                            return ValueTask.CompletedTask;
                        }

                        var reconstructionEntries = archiveEntries.Where(entry => entry == currentInitEntry ||
                            (entry.EntryType != ChangeTrackingEntryTypes.Init && entry.Timestamp >= currentInitEntry.Timestamp)).ToList();
                        ct.ThrowIfCancellationRequested();
                        var reconstruction = SourceTrackingRebase.ReassemblePrefix(reconstructionEntries);
                        if (reconstruction.FailedAt.HasValue)
                        {
                            incompleteReason = $"Partial rebase: stopped before tracking timestamp {reconstruction.FailedAt.Value:O}. " +
                                $"The failing timestamp group and all later entries were retained. {reconstruction.FailureReason}";
                            if (reconstruction.Entity == null)
                            {
                                results.Add(new RebaseEntityTrackingResult()
                                {
                                    DataSource = dataSource,
                                    EntityType = entityType,
                                    EntityId = entityId,
                                    Success = false,
                                    FailureReason = incompleteReason,
                                    ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                                });
                                return ValueTask.CompletedTask;
                            }

                            archiveEntries = archiveEntries.Where(entry => entry.Timestamp < reconstruction.FailedAt.Value).ToList();
                        }
                        rebaseTo = reconstruction.RebasedTo;

                        InitTrackedEntityRequest newInitEntry = null;
                        if (currentInitEntry.Timestamp == rebaseTo &&
                            !archiveEntries.Any(entry => entry.EntryType == ChangeTrackingEntryTypes.Update && entry.Timestamp >= currentInitEntry.Timestamp))
                        {
                            archiveEntries.Remove(currentInitEntry);
                        }
                        else
                        {
                            newInitEntry = new InitTrackedEntityRequest()
                            {
                                DataSource = dataSource,
                                EntityType = entityType,
                                EntityData = RebaseTrackingPersistence.CreateBaselineData(reconstruction.Entity, currentInitEntry),
                                EntityId = entityId,
                                Timestamp = rebaseTo
                            };
                        }

                        plans.Add(new RebaseTrackingPlan(newInitEntry, archiveEntries, new RebaseEntityTrackingResult()
                        {
                            DataSource = dataSource,
                            EntityType = entityType,
                            EntityId = entityId,
                            RebasedTo = rebaseTo,
                            ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                        }, incompleteReason, newInitEntry == null ? currentInitEntry : null,
                            IncludeArchivedTrackingEntries: request.IncludeArchivedTrackingEntries));
                        return ValueTask.CompletedTask;
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
                        DataSource = storedIdentities.Count == 1 ? storedIdentities[0].DataSource : request.DataSource,
                        EntityType = storedIdentities.Count == 1 ? storedIdentities[0].EntityType : request.EntityType,
                        EntityId = entityId,
                        Success = false,
                        FailureReason = lockFailure,
                        ArchivedTrackingEntries = new List<ChangeTrackingEntry>()
                    });
            }

            entitiesToProcess.RemoveRange(0, batch.Count);
        }



        return new ProcessRebaseSourceEntitiesResponse()
        {
            Results = results.ToList()
        };
    }
}

