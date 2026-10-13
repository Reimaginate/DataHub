using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.CalculateSourceEntityUpdates;
using Reimaginate.DataHub.Requests.Internal.CreateDeferredEntityResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdatedEntities;
using Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.ResolveExternalEntityReferences;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;


namespace Reimaginate.DataHub.Requests.Internal.MergeExistingEntities;

public class MergeExistingEntitiesRequestHandler(IIdService idService, IMediator mediator) : IHandler<MergeExistingEntitiesRequest, MergeExistingEntitiesResponse>
{
    private const int BatchSize = 5000;

    public async Task<MergeExistingEntitiesResponse> HandleAsync(MergeExistingEntitiesRequest request, CancellationToken cancellationToken)
    {
        var (results, mergeRequests, resolvedReferencedEntities) = InitializeVars(request);

        while (mergeRequests.Any())
        {
            var batch = mergeRequests.Take(BatchSize).ToList();
            var batchResults = await ProcessMergeRequestBatch(batch, request, mergeRequests, cancellationToken);
            results.AddRange(batchResults);
            mergeRequests.RemoveRange(0, batch.Count);
        }

        var resultingDataHubEntities = results.Where(w => w.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated)
            .GroupBy(r => r.DataHubEntityId).Select(g => g.Last().ResultingDataHubEntity).ToList();

        var dataHubEntitiesToResolveReferences = resultingDataHubEntities.Where(entity => entity
                .ExternalEntityReferences()
                .Any()
        ).ToList();

        #region Resolve External Entity References

        if (dataHubEntitiesToResolveReferences.Any())
        {
            var externalEntityResolutionResponse = (await mediator.TrySend(new ResolveExternalEntityReferencesRequest()
            {
                DataHubEntitiesToResolve = dataHubEntitiesToResolveReferences
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var updatedDataHubEntities = externalEntityResolutionResponse.UpdatedDataHubEntities;
            resultingDataHubEntities.Replace(updatedDataHubEntities, w => w.DataHubEntityId());

            updatedDataHubEntities.ForEach(e =>
            {
                var matchingMergeResult = results.FirstOrDefault(f => f.DataHubEntityId == e.DataHubEntityId());
                if (matchingMergeResult != null)
                {
                    matchingMergeResult.ResultingDataHubEntity = e;
                }
            });

            dataHubEntitiesToResolveReferences = resultingDataHubEntities.Where(entity => entity
                .ExternalEntityReferences()
                .Any()
            ).ToList();
        }

        #endregion

        #region Create deferred entity resolutions for any unresolved entity references

        if (dataHubEntitiesToResolveReferences.Any())
        {
            _ = (await mediator.SendAsync(new CreateDeferredEntityResolutionPromisesRequest()
            {
                Entities = dataHubEntitiesToResolveReferences
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
        }

        #endregion

        #region Resolve deferred entity reference resolutions for newly created ResultingEntity Hub entities referenced by existing ResultingEntity Hub entities

        if (resolvedReferencedEntities.Any())
        {
            _ = (await mediator.SendAsync(new ResolveEntityReferenceResolutionPromisesRequest()
            {
                SourceSystemEntityIds = request.MergeRequests.Select(s => s.SourceEntityId).ToList(),
                ResolvedReferencedEntities = resolvedReferencedEntities
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
        }

        #endregion

        return new MergeExistingEntitiesResponse()
        {
            Results = results
        };
    }



    #region Private helpers

    private async Task<List<MergeEntityResult>> ProcessMergeRequestBatch(List<MergeEntityRequest> mergeRequestsBatch, MergeExistingEntitiesRequest request, List<MergeEntityRequest> mergeRequests, CancellationToken cancellationToken)
    {
        var initialisationsByInput = new List<InitTrackedEntityRequest>[mergeRequestsBatch.Count];
        var updatesByInput = new List<AddTrackedEntityChangeSetRequest>[mergeRequestsBatch.Count];
        var sourceEntityIds = mergeRequestsBatch.Select(s => s.SourceEntityId).Distinct().ToList();
        var results = new ConcurrentBag<MergeEntityResult>();

        var sourceSystemEntityTrackingEntries = await SourceEntityTrackingEntries(request, cancellationToken, sourceEntityIds);
        var trackingEntriesBySource = sourceSystemEntityTrackingEntries.ToLookup(e => e.EntityId);

        #region CalculateChangesToSourceEntities

        // Calculate successive deltas in source timestamp order. Independent sources
        // still run concurrently; flatten their results by original ordinal afterwards.
        var sourceGroups = mergeRequestsBatch.Select((item, index) => (Request: item, Index: index))
            .GroupBy(item => item.Request.SourceEntityId);
        await Parallel.ForEachAsync(sourceGroups, new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        }, async (sourceGroup, ct) =>
        {
            var changeTrackingCache = trackingEntriesBySource[sourceGroup.Key].ToList();
            foreach (var item in sourceGroup)
            {
                var mergeRequest = item.Request;
                try
                {
                    var calculateSourceEntityUpdatesResponse = (await mediator.TrySend(new CalculateSourceEntityUpdatesRequest()
                    {
                        DataSource = mergeRequest.DataSource,
                        SourceEntityType = mergeRequest.SourceEntityType,
                        SourceEntityId = mergeRequest.SourceEntityId,
                        SourceEntity = mergeRequest.Data,
                        RetainUnchangedUpdate = sourceGroup.Count() > 1,
                        OrderedChangeTrackingEntryCache = changeTrackingCache
                    }, ct)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    initialisationsByInput[item.Index] = calculateSourceEntityUpdatesResponse.ResultingInitTrackedEntityRequests;
                    updatesByInput[item.Index] = calculateSourceEntityUpdatesResponse.ResultingSourceEntityUpdates;
                    foreach (var initialisation in initialisationsByInput[item.Index])
                    {
                        // Make a newly initialised source visible to later items in this batch.
                        changeTrackingCache.Add(new ChangeTrackingEntry
                        {
                            EntryType = ChangeTrackingEntryTypes.Init,
                            DataSource = initialisation.DataSource,
                            EntityType = initialisation.EntityType,
                            EntityId = initialisation.EntityId,
                            Timestamp = initialisation.Timestamp.Value,
                            Data = (JObject)initialisation.EntityData.DeepClone()
                        });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new MergeEntityResult()
                    {
                        DataSource = mergeRequest?.DataSource,
                        SourceEntityType = mergeRequest?.SourceEntityType,
                        SourceEntityId = mergeRequest?.SourceEntityId,
                        DataHubEntityType = mergeRequest?.DataHubEntityType,
                        MergeOutcome = MergeOutcomes.MergeFailed,
                        FailureReason = ex.Message
                    });
                }
            }
        });

        var initTrackedEntityRequests = initialisationsByInput.Where(items => items != null).SelectMany(items => items).ToList();
        var sourceSystemEntityUpdates = updatesByInput.Where(items => items != null).SelectMany(items => items).ToList();

        #endregion

        #region Initialize Change Tracking For Untracked Source Entities

        if (initTrackedEntityRequests.Any())
        {
            _ = (await mediator.SendAsync(new InitTrackedEntitiesRequest()
            {
                Requests = initTrackedEntityRequests.ToList()
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
        }

        #endregion

        if (sourceSystemEntityUpdates.Any())
        {
            var sourceChangesByTrackingEntry = new Dictionary<ChangeTrackingEntry, AddTrackedEntityChangeSetRequest>();
            var dataHubChangeTrackingEntries = await ConvertSourceEntityChangesToDataHubChangeTrackingEntries(sourceSystemEntityUpdates, request, sourceChangesByTrackingEntry, cancellationToken);

            var alternateKeyUpdates = sourceSystemEntityUpdates.Where(w => w.ChangeSet.ContainsKey(nameof(DataHubEntity.alternateKeys))
                                                                           && w.ChangeSet[nameof(DataHubEntity.alternateKeys)] != null
                                                                           && w.ChangeSet[nameof(DataHubEntity.alternateKeys)]!.HasValues
            ).ToList();

            if (dataHubChangeTrackingEntries.Any(e => e.Data.HasValues) || alternateKeyUpdates.Any())
            {
                var batchResults = await ProcessUpdatedEntities(request, mergeRequests, dataHubChangeTrackingEntries, alternateKeyUpdates, sourceChangesByTrackingEntry, cancellationToken);
                batchResults.ForEach(e => results.Add(e));
            }
        }

        var sourceEntitiesWithFailedUpdates = results.Where(w => MergeOutcomes.IsFailure(w.MergeOutcome)).Select(s => s.SourceEntityId).ToList();
        var sourceSystemEntitiesWithoutUpdates = mergeRequestsBatch.Select(s => s.SourceEntityId).Except(sourceEntitiesWithFailedUpdates).Except(results.Select(s => s.SourceEntityId)).ToList();

        if (sourceSystemEntitiesWithoutUpdates.Any())
        {
            var dataHubEntitiesWithSourceEntityAlternateKeys = await DataHubEntitiesWithSourceEntityAlternateKeys(request, cancellationToken, sourceSystemEntitiesWithoutUpdates);

            Parallel.ForEach(sourceSystemEntitiesWithoutUpdates, sourceEntityId =>
            {
                #region Add Merge Result For Source Entities Without Updates

                var mergeRequest = request.MergeRequests.First(f => f.SourceEntityId == sourceEntityId);
                var dataHubEntity = dataHubEntitiesWithSourceEntityAlternateKeys.FirstOrDefault(f => f.SourceEntityReference.DataSource == request.DataSource
                                                                                                     && f.SourceEntityReference.SourceEntityType == request.SourceEntityType
                                                                                                     && f.SourceEntityReference.EntityId == sourceEntityId);
                results.Add(new MergeEntityResult()
                {
                    DataSource = mergeRequest.DataSource,
                    SourceEntityType = mergeRequest.SourceEntityType,
                    DataHubEntityType = mergeRequest.DataHubEntityType,
                    SourceEntityId = sourceEntityId,
                    DataHubEntityId = dataHubEntity?.DataHubEntityReference.EntityId,
                    MergeOutcome = MergeOutcomes.NoSourceEntityUpdateToProcess
                });

                #endregion
            });
        }

        await CommitSourceEntityUpdatesToDb(cancellationToken, sourceSystemEntityUpdates, sourceSystemEntityTrackingEntries, initTrackedEntityRequests, request, results);

        return results.ToList();
    }

    private async Task CommitSourceEntityUpdatesToDb(CancellationToken cancellationToken, List<AddTrackedEntityChangeSetRequest> sourceSystemEntityUpdates,
        List<ChangeTrackingEntry> originalTrackingEntries, List<InitTrackedEntityRequest> initialisations, MergeExistingEntitiesRequest request, ConcurrentBag<MergeEntityResult> results)
    {
        // Only a skipped change followed by an accepted change needs rebasing. Ordinary
        // batches and entirely successful repeated-source batches retain their deltas.
        var excludedSourceIds = sourceSystemEntityUpdates.Where(s => s.ExcludeFromSourceHistory).Select(s => s.SourceEntityId).ToHashSet();
        ILookup<string, ChangeTrackingEntry> originalsBySource = null;
        ILookup<string, InitTrackedEntityRequest> initialisationsBySource = null;
        foreach (var group in sourceSystemEntityUpdates.Where(s => excludedSourceIds.Contains(s.SourceEntityId)).GroupBy(s => s.SourceEntityId))
        {
            var afterSkippedChange = false;
            JObject working = null;
            foreach (var update in group)
            {
                if (update.ExcludeFromSourceHistory)
                {
                    afterSkippedChange = true;
                    continue;
                }
                if (!afterSkippedChange) continue;

                if (working == null)
                {
                    originalsBySource ??= originalTrackingEntries.ToLookup(e => e.EntityId);
                    initialisationsBySource ??= initialisations.ToLookup(i => i.EntityId);
                    var priorEntries = originalsBySource[group.Key].ToList();
                    priorEntries.AddRange(initialisationsBySource[group.Key].Select(i => new ChangeTrackingEntry
                    {
                        EntryType = ChangeTrackingEntryTypes.Init, EntityId = i.EntityId, EntityType = i.EntityType,
                        DataSource = i.DataSource, Timestamp = i.Timestamp.Value, Data = i.EntityData
                    }));
                    priorEntries.AddRange(group.TakeWhile(s => !ReferenceEquals(s, update)).Where(s => !s.ExcludeFromSourceHistory).Select(s => new ChangeTrackingEntry
                    {
                        EntryType = ChangeTrackingEntryTypes.Update, EntityId = s.SourceEntityId, EntityType = s.EntityType,
                        DataSource = s.DataSource, Timestamp = s.TimeStamp, Data = s.ChangeSet
                    }));
                    working = ChangeTrackingHelper.ReassembleEntity(priorEntries);
                }

                // Recalculate from the accepted state and fields actually supplied by
                // this input. Omitted fields from a rejected observation stay omitted.
                var next = update.SourceEntityInput == null
                    ? MergeBatchIntegrity.ApplyChange(working, update.ChangeSet)
                    : MergeBatchIntegrity.MergeInput(working, update.SourceEntityInput);
                update.ChangeSet = ChangeTrackingHelper.StripBaseProperties(
                    ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(working, next) as JObject) ?? new JObject();
                working = next;
            }
        }
        var successfullyMergedChangeSets = sourceSystemEntityUpdates.Where(w => !w.ExcludeFromSourceHistory && w.ChangeSet.HasValues).ToList();
        if (successfullyMergedChangeSets.Any())
        {
            var response = (await mediator.SendAsync(new UpsertCosmosDocumentsCommand<ChangeTrackingEntry>()
            {
                Documents = successfullyMergedChangeSets.Select(s => new ChangeTrackingEntry()
                {
                    id = idService.NewId<ChangeTrackingEntry>(),
                    EntryType = ChangeTrackingEntryTypes.Update,
                    EntityType = s.EntityType,
                    EntityId = s.SourceEntityId,
                    DataSource = s.DataSource,
                    Timestamp = s.TimeStamp,
                    Data = ChangeTrackingHelper.StripBaseProperties(JObject.FromObject(s.ChangeSet))
                }).ToList()
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
            foreach (var failure in response.Failures)
            {
                results.Add(new MergeEntityResult
                {
                    DataSource = request.DataSource, SourceEntityType = request.SourceEntityType,
                    DataHubEntityType = request.DataHubEntityType, SourceEntityId = failure.Item.EntityId,
                    DataHubEntityId = request.ResolvedDataHubEntities.First(r => r.SourceEntityReference.EntityId == failure.Item.EntityId).DataHubEntityReference.EntityId,
                    MergeOutcome = MergeOutcomes.MergeFailed, FailureReason = failure.Error?.Message
                });
            }
        }
    }

    private async Task<List<ResolvedEntityReference>> DataHubEntitiesWithSourceEntityAlternateKeys(MergeExistingEntitiesRequest request, CancellationToken cancellationToken, List<string> sourceSystemEntitiesWithoutUpdates)
    {
        var resolveEntityReferencesResponse = (await mediator.TrySend(new ResolveEntityReferencesRequest()
        {
            EntityReferences = sourceSystemEntitiesWithoutUpdates.Select(s => new ExternalEntityReference()
            {
                DataSource = request.DataSource,
                EntityType = request.DataHubEntityType,
                SourceEntityType = request.SourceEntityType,
                EntityId = s
            }).ToList()
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var dataHubEntitiesWithSourceEntityAlternateKeys = resolveEntityReferencesResponse.Results;
        return dataHubEntitiesWithSourceEntityAlternateKeys;
    }

    private async Task<List<MergeEntityResult>> ProcessUpdatedEntities(MergeExistingEntitiesRequest request, List<MergeEntityRequest> mergeRequests, List<ChangeTrackingEntry> convertedSourceEntityChanges, List<AddTrackedEntityChangeSetRequest> changesToAlternateKeys,
        Dictionary<ChangeTrackingEntry, AddTrackedEntityChangeSetRequest> sourceChangesByTrackingEntry, CancellationToken cancellationToken)
    {
        var mergeDataHubEntityUpdatesResponse = (await mediator.TrySend(new ProcessUpdatedEntitiesRequest()
        {
            CorrelationId = request.CorrelationId,
            ConvertedSourceEntityChanges = convertedSourceEntityChanges,
            SourceChangesByTrackingEntry = sourceChangesByTrackingEntry,
            DataHubEntityType = request.DataHubEntityType,
            DataSource = request.DataSource,
            MergeRequests = mergeRequests,
            ResolvedReferencedEntities = request.ResolvedDataHubEntities,
            SourceEntityChangesWithAlternateKeys = changesToAlternateKeys,
            SourceEntityType = request.SourceEntityType,
            EntityConfig = request.EntityConfig
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return mergeDataHubEntityUpdatesResponse.Results;
    }

    private static List<AddTrackedEntityChangeSetRequest> ExtractUpdatesToAlternateKeys(List<AddTrackedEntityChangeSetRequest> sourceSystemEntityUpdates)
    {
        var changesToAlternateKeys = sourceSystemEntityUpdates.Where(w => w.ChangeSet.ContainsKey(nameof(DataHubEntity.alternateKeys))
                                                                          && w.ChangeSet[nameof(DataHubEntity.alternateKeys)] != null
                                                                          && w.ChangeSet[nameof(DataHubEntity.alternateKeys)]!.HasValues
        ).ToList();
        return changesToAlternateKeys;
    }

    private Task<List<ChangeTrackingEntry>> ConvertSourceEntityChangesToDataHubChangeTrackingEntries(List<AddTrackedEntityChangeSetRequest> sourceSystemEntityUpdates, MergeExistingEntitiesRequest request,
        Dictionary<ChangeTrackingEntry, AddTrackedEntityChangeSetRequest> sourceChangesByTrackingEntry, CancellationToken cancellationToken)
    {
        var newChangeTrackingEntry = sourceSystemEntityUpdates.Select(s =>
        {
            var sanitizedChangeSet = (JObject)s.ChangeSet.DeepClone();
            sanitizedChangeSet.Remove(nameof(DataHubEntity.alternateKeys));
            sanitizedChangeSet = ChangeTrackingHelper.StripBaseProperties(sanitizedChangeSet);

            // Key-only events take the same staged acceptance path as property events.
            if (!sanitizedChangeSet.HasValues && !s.ChangeSet.ContainsKey(nameof(DataHubEntity.alternateKeys)) && s.SourceEntityInput == null) return null;

            var entry = new ChangeTrackingEntry()
            {
                id = idService.NewId<ChangeTrackingEntry>(),
                EntryType = ChangeTrackingEntryTypes.Update,
                EntityType = request.DataHubEntityType,
                EntityId = request.ResolvedDataHubEntities.First(f => f.SourceEntityReference.EntityId == s.SourceEntityId).DataHubEntityReference.EntityId,
                DataSource = DataSources.DataHub,
                Timestamp = s.TimeStamp,
                Data = sanitizedChangeSet
            };
            sourceChangesByTrackingEntry.Add(entry, s);
            return entry;
        }).Where(w => w != null).ToList();

        return Task.FromResult(newChangeTrackingEntry);
    }

    private async Task<List<ChangeTrackingEntry>> SourceEntityTrackingEntries(MergeExistingEntitiesRequest request, CancellationToken cancellationToken, List<string> sourceEntityIds)
    {
        var getTrackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest()
        {
            DataSource = request.DataSource,
            EntityType = request.SourceEntityType,
            EntityIds = sourceEntityIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var sourceSystemEntityTrackingEntries = getTrackingEntriesResponse.TrackingEntries;
        return sourceSystemEntityTrackingEntries;
    }

    private static (List<MergeEntityResult> results, List<MergeEntityRequest> mergeRequests, List<ResolvedEntityReference> resolvedReferencedEntities) InitializeVars(MergeExistingEntitiesRequest request)
    {
        var results = new List<MergeEntityResult>();
        var mergeRequests = MergeRequestOrdering.SortSourceVersions(request.MergeRequests, results);
        mergeRequests = MergeBatchIntegrity.ExcludeConflictingSources(mergeRequests, request.ResolvedDataHubEntities, results);
        return (results, mergeRequests, request.ResolvedDataHubEntities);
    }

    #endregion

}
