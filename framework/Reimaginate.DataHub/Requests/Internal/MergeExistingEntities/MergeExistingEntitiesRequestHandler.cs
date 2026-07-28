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

        var resultingDataHubEntities = results.Where(w => w.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated).Select(s => s.ResultingDataHubEntity).ToList();

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
        var initTrackedEntityRequests = new ConcurrentBag<InitTrackedEntityRequest>();
        var sourceSystemEntityUpdates = new ConcurrentBag<AddTrackedEntityChangeSetRequest>();
        var sourceEntityIds = mergeRequestsBatch.Select(s => s.SourceEntityId).ToList();
        var results = new ConcurrentBag<MergeEntityResult>();

        var sourceSystemEntityTrackingEntries = await SourceEntityTrackingEntries(request, cancellationToken, sourceEntityIds);
        var changeTrackingCache = new ConcurrentBag<ChangeTrackingEntry>(sourceSystemEntityTrackingEntries);

        #region CalculateChangesToSourceEntities

        await Parallel.ForEachAsync(mergeRequestsBatch, new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        }, async (mergeRequest, ct) =>
        {
            try
            {
                var calculateSourceEntityUpdatesResponse = (await mediator.TrySend(new CalculateSourceEntityUpdatesRequest()
                {
                    DataSource = mergeRequest.DataSource,
                    SourceEntityType = mergeRequest.SourceEntityType,
                    SourceEntityId = mergeRequest.SourceEntityId,
                    SourceEntity = mergeRequest.Data,
                    ChangeTrackingEntryCache = changeTrackingCache
                }, ct)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                calculateSourceEntityUpdatesResponse.ResultingInitTrackedEntityRequests.ForEach(e => initTrackedEntityRequests.Add(e));
                calculateSourceEntityUpdatesResponse.ResultingSourceEntityUpdates.ForEach(e => sourceSystemEntityUpdates.Add(e));
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
        });

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
            var dataHubChangeTrackingEntries = await ConvertSourceEntityChangesToDataHubChangeTrackingEntries(sourceSystemEntityUpdates.ToList(), request, cancellationToken);

            var alternateKeyUpdates = sourceSystemEntityUpdates.Where(w => w.ChangeSet.ContainsKey(nameof(DataHubEntity.alternateKeys))
                                                                           && w.ChangeSet[nameof(DataHubEntity.alternateKeys)] != null
                                                                           && w.ChangeSet[nameof(DataHubEntity.alternateKeys)]!.HasValues
            ).ToList();

            if (dataHubChangeTrackingEntries.Any() || alternateKeyUpdates.Any())
            {
                var batchResults = await ProcessUpdatedEntities(request, mergeRequests, dataHubChangeTrackingEntries, alternateKeyUpdates, cancellationToken);
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

        await CommitSourceEntityUpdatesToDb(cancellationToken, sourceSystemEntityUpdates.ToList(), sourceEntitiesWithFailedUpdates);

        return results.ToList();
    }

    private async Task CommitSourceEntityUpdatesToDb(CancellationToken cancellationToken, List<AddTrackedEntityChangeSetRequest> sourceSystemEntityUpdates, List<string> sourceEntitiesWithFailedUpdates)
    {
        var successfullyMergedChangeSets = sourceSystemEntityUpdates.Where(w => !sourceEntitiesWithFailedUpdates.Contains(w.SourceEntityId)).ToList();
        if (successfullyMergedChangeSets.Any())
        {
            _ = (await mediator.SendAsync(new UpsertCosmosDocumentsCommand<ChangeTrackingEntry>()
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

    private async Task<List<MergeEntityResult>> ProcessUpdatedEntities(MergeExistingEntitiesRequest request, List<MergeEntityRequest> mergeRequests, List<ChangeTrackingEntry> convertedSourceEntityChanges, List<AddTrackedEntityChangeSetRequest> changesToAlternateKeys, CancellationToken cancellationToken)
    {
        var mergeDataHubEntityUpdatesResponse = (await mediator.TrySend(new ProcessUpdatedEntitiesRequest()
        {
            CorrelationId = request.CorrelationId,
            ConvertedSourceEntityChanges = convertedSourceEntityChanges,
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

    private Task<List<ChangeTrackingEntry>> ConvertSourceEntityChangesToDataHubChangeTrackingEntries(List<AddTrackedEntityChangeSetRequest> sourceSystemEntityUpdates, MergeExistingEntitiesRequest request, CancellationToken cancellationToken)
    {
        var newChangeTrackingEntry = sourceSystemEntityUpdates.Select(s =>
        {
            var sanitizedChangeSet = (JObject)s.ChangeSet.DeepClone();
            sanitizedChangeSet.Remove(nameof(DataHubEntity.alternateKeys));
            sanitizedChangeSet = ChangeTrackingHelper.StripBaseProperties(sanitizedChangeSet);

            if (!sanitizedChangeSet.HasValues) return null;

            return new ChangeTrackingEntry()
            {
                id = idService.NewId<ChangeTrackingEntry>(),
                EntryType = ChangeTrackingEntryTypes.Update,
                EntityType = request.DataHubEntityType,
                EntityId = request.ResolvedDataHubEntities.First(f => f.SourceEntityReference.EntityId == s.SourceEntityId).DataHubEntityReference.EntityId,
                DataSource = DataSources.DataHub,
                Timestamp = s.TimeStamp,
                Data = sanitizedChangeSet
            };
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
        var mergeRequests = new List<MergeEntityRequest>(request.MergeRequests);
        return (results, mergeRequests, request.ResolvedDataHubEntities);
    }

    #endregion

}
