using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.CreateDeferredEntityResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.FindPotentialDuplicates;
using Reimaginate.DataHub.Requests.Internal.ProcessNewEntity;
using Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.ResolveExternalEntityReferences;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Rules;
using Reimaginate.Mediator;


namespace Reimaginate.DataHub.Requests.Internal.MergeNewEntities;

public class MergeNewEntitiesRequestHandler(IMediator mediator, ITimeService timeService) : IHandler<MergeNewEntitiesRequest, MergeNewEntitiesResponse>
{
    private const int BatchSize = 5000;

    public async Task<MergeNewEntitiesResponse> HandleAsync(MergeNewEntitiesRequest request, CancellationToken cancellationToken)
    {
        var (successes, failures, resolvedReferencedEntities) = InitializeVars(request);

        var mergeRequests = new List<MergeEntityRequest>(request.MergeRequests);
        while (mergeRequests.Any())
        {
            var mergeRequestsBatch = mergeRequests.Take(BatchSize).ToList();
            await ProcessMergeRequestBatch(request, mergeRequestsBatch, failures, resolvedReferencedEntities, successes, cancellationToken);
            mergeRequests.RemoveRange(0, mergeRequestsBatch.Count);
        }

        var resultingDataHubEntities = successes
            .Where(s => s.MergeOutcome != MergeOutcomes.MergeSilentlyRejected && s.ResultingDataHubEntity != null)
            .Select(s => s.ResultingDataHubEntity)
            .ToList();

        var dataHubEntitiesToResolveReferences = resultingDataHubEntities.Where(entity => entity
                .ExternalEntityReferences()
                .Any()
        ).ToList();

        #region Resolve External Entity References

        if (dataHubEntitiesToResolveReferences.Any())
        {
            var externalEntityResolutionResponse = (await mediator.TrySend(new ResolveExternalEntityReferencesRequest()
            {
                DataHubEntitiesToResolve = dataHubEntitiesToResolveReferences,
                DoNotTrack = request.DoNotTrack
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var updatedDataHubEntities = externalEntityResolutionResponse.UpdatedDataHubEntities;
            resultingDataHubEntities.Replace(updatedDataHubEntities, w => w.DataHubEntityId());

            updatedDataHubEntities.ForEach(e =>
            {
                var matchingMergeResult = successes.FirstOrDefault(f => f.DataHubEntityId == e.DataHubEntityId());
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
                ResolvedReferencedEntities = resolvedReferencedEntities,
                DoNotTrack = request.DoNotTrack
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
        }

        #endregion

        return new MergeNewEntitiesResponse()
        {
            Successes = successes,
            Failures = failures
        };
    }

    #region Private helpers

    private async Task ProcessMergeRequestBatch(MergeNewEntitiesRequest request, List<MergeEntityRequest> mergeRequestsBatch, List<MergeEntityResult> failures, List<ResolvedEntityReference> resolvedReferencedEntities, List<MergeEntityResult> successes, CancellationToken cancellationToken)
    {
        var mergeResultsBatch = new List<MergeEntityResult>();
        var entityConfig = request.EntityConfig;
        var changeTrackingEntries = new List<ChangeTrackingEntry>();

        var duplicatePreventionRule = GetDuplicatePreventionRuleForDataSource(request);
        var potentialDuplicates = await GetPotentialDuplicates(request, cancellationToken, mergeRequestsBatch, duplicatePreventionRule) ?? new JArray();

        foreach (var mergeRequest in mergeRequestsBatch)
        {
            try
            {
                var processEntityResponse = await ProcessNewEntity(mergeRequest, duplicatePreventionRule, potentialDuplicates, entityConfig, request.DoNotTrack, cancellationToken);
                if (!Successful(processEntityResponse))
                {
                    failures.Add(processEntityResponse.MergeEntityResult);
                    continue;
                }

                mergeResultsBatch.Add(processEntityResponse.MergeEntityResult);
                if (processEntityResponse.ResultingChangeTrackingEntries != null)
                {
                    changeTrackingEntries.AddRange(processEntityResponse.ResultingChangeTrackingEntries);
                }

                if (processEntityResponse.MergeEntityResult.MergeOutcome == MergeOutcomes.MergeSilentlyRejected)
                {
                    successes.Add(processEntityResponse.MergeEntityResult);
                    continue;
                }

                var resultingEntity = ProcessResultingEntity(resolvedReferencedEntities, processEntityResponse, mergeRequest, out var resultingEntityId);
                AddResultingEntityToPotentialDuplicates(potentialDuplicates, resultingEntityId, resultingEntity);
            }
            catch (Exception ex)
            {
                HandleMergeFailure(failures, mergeRequest, ex);
            }
        }

        var newDataHubEntities = mergeResultsBatch.Where(s => s.MergeOutcome == MergeOutcomes.NewEntityCreated).ToList();
        await CommitInitChangeTrackingToDb(changeTrackingEntries.Where(w => w.EntryType == ChangeTrackingEntryTypes.Init).ToList(), cancellationToken);
        await CommitNewDataHubEntitiesToDb(newDataHubEntities, successes, failures, cancellationToken);

        var updatedDataHubEntities = mergeResultsBatch.Where(s => s.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated).ToList();
        await CommitUpdateChangeTrackingToDb(changeTrackingEntries.Where(w => w.EntryType == ChangeTrackingEntryTypes.Update).ToList(), cancellationToken);
        await CommitUpdatedDataHubEntitiesToDb(cancellationToken, successes, updatedDataHubEntities);
    }

    private (List<MergeEntityResult> successes, List<MergeEntityResult> failures, List<ResolvedEntityReference> resolvedReferencedEntities) InitializeVars(MergeNewEntitiesRequest request)
    {
        var successes = new List<MergeEntityResult>();
        var failures = new List<MergeEntityResult>();
        var resolvedReferencedEntities = request.ResolvedDataHubEntities ??= new List<ResolvedEntityReference>();
        return (successes, failures, resolvedReferencedEntities);
    }

    private DuplicatePreventionRule GetDuplicatePreventionRuleForDataSource(MergeNewEntitiesRequest request)
    {
        var duplicatePreventionRule = request.EntityConfig?.DuplicatePreventionRules.FirstOrDefault(x => string.Equals(x.DataSource, request.DataSource, StringComparison.CurrentCultureIgnoreCase) || x.DataSource == "*");
        return duplicatePreventionRule;
    }

    private async Task<JArray> GetPotentialDuplicates(MergeNewEntitiesRequest request, CancellationToken cancellationToken, List<MergeEntityRequest> mergeRequestsBatch, DuplicatePreventionRule duplicatePreventionRule)
    {
        if (duplicatePreventionRule == null) return new JArray();

        JArray potentialDuplicates = null;
        var findPotentialDuplicatesResponse = (await mediator.TrySend(new FindPotentialDuplicatesRequest()
        {
            DataHubEntityType = request.DataHubEntityType,
            DuplicatePreventionRule = duplicatePreventionRule,
            EntitiesToMatch = mergeRequestsBatch.Select(x => x.Data).ToList()
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        potentialDuplicates = findPotentialDuplicatesResponse.PotentialDuplicates;

        return potentialDuplicates;
    }

    private async Task<ProcessNewEntityResponse> ProcessNewEntity(MergeEntityRequest mergeRequest, DuplicatePreventionRule duplicatePreventionRule, JArray potentialDuplicates, EntityConfig entityConfig, bool doNotTrack = false, CancellationToken cancellationToken = default)
    {
        var mergeNewEntityResponse = (await mediator.TrySend(new ProcessNewEntityRequest()
        {
            DataSource = mergeRequest.DataSource,
            SourceEntityType = mergeRequest.SourceEntityType,
            SourceEntityId = mergeRequest.SourceEntityId,
            DataHubEntityType = mergeRequest.DataHubEntityType,
            SourceEntity = mergeRequest.Data,
            DuplicatePreventionRule = duplicatePreventionRule,
            PotentialDuplicates = potentialDuplicates,
            EntityConfig = entityConfig,
            DoNotTrack = doNotTrack
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        return mergeNewEntityResponse;
    }

    private bool Successful(ProcessNewEntityResponse processNewEntityResponse)
    {
        return !MergeOutcomes.IsFailure(processNewEntityResponse.MergeEntityResult.MergeOutcome);
    }

    private JObject ProcessResultingEntity(List<ResolvedEntityReference> resolvedReferencedEntities, ProcessNewEntityResponse processNewEntityResponse, MergeEntityRequest mergeRequest, out string resultingEntityId)
    {
        var resultingEntity = processNewEntityResponse.MergeEntityResult.ResultingDataHubEntity;
        resultingEntityId = resultingEntity.DataHubEntityId()!;
        resolvedReferencedEntities.Add(new ResolvedEntityReference()
        {
            SourceEntityReference = new ExternalEntityReference()
            {
                DataSource = mergeRequest.DataSource,
                EntityId = mergeRequest.SourceEntityId,
                EntityType = mergeRequest.DataHubEntityType,
                SourceEntityType = mergeRequest.SourceEntityType,
            },
            DataHubEntityReference = new EntityReference()
            {
                EntityType = mergeRequest.DataHubEntityType,
                EntityId = resultingEntityId,
            }
        });
        return resultingEntity;
    }

    private void AddResultingEntityToPotentialDuplicates(JArray potentialDuplicates, string resultingEntityId, JObject resultingEntity)
    {
        if (potentialDuplicates.All(a => a.DataHubEntityId() != resultingEntityId)) potentialDuplicates.Add(resultingEntity);
    }

    private void HandleMergeFailure(List<MergeEntityResult> failures, MergeEntityRequest mergeRequest, Exception ex)
    {
        failures.Add(new MergeEntityResult()
        {
            DataSource = mergeRequest?.DataSource,
            SourceEntityType = mergeRequest?.SourceEntityType,
            SourceEntityId = mergeRequest?.SourceEntityId,
            DataHubEntityType = mergeRequest?.DataHubEntityType,
            MergeOutcome = MergeOutcomes.MergeFailed,
            FailureReason = ex?.Message
        });
    }

    private async Task CommitInitChangeTrackingToDb(List<ChangeTrackingEntry> changeTrackingEntries, CancellationToken cancellationToken)
    {
        if (!changeTrackingEntries.Any()) return;

        var createRequest = new CreateCosmosDocumentsCommand<ChangeTrackingEntry>()
        {
            Documents = changeTrackingEntries
        };

        var createResponse = (await mediator.TrySend(createRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (createResponse.Failures.Any())
        {
            throw new AggregateException(createResponse.Failures.Select(s => s.Error));
        }
    }

    private async Task CommitUpdateChangeTrackingToDb(List<ChangeTrackingEntry> changeTrackingEntries, CancellationToken cancellationToken)
    {
        if (!changeTrackingEntries.Any()) return;

        var createRequest = new CreateCosmosDocumentsCommand<ChangeTrackingEntry>()
        {
            Documents = changeTrackingEntries
        };

        var createResponse = (await mediator.TrySend(createRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (createResponse.Failures.Any())
        {
            throw new AggregateException(createResponse.Failures.Select(s => s.Error));
        }
    }

    private async Task CommitNewDataHubEntitiesToDb(List<MergeEntityResult> newDataHubEntities, List<MergeEntityResult> successes, List<MergeEntityResult> failures, CancellationToken cancellationToken)
    {
        if (newDataHubEntities.Any())
        {
            newDataHubEntities.ForEach(e => e.ResultingDataHubEntity[nameof(DataHubEntity.lastUpdated)] = timeService.Now());

            var upsertResponse = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
            {
                Entities = newDataHubEntities.Select(s => s.ResultingDataHubEntity.RemoveNullValues()).ToList()
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (upsertResponse.Failures.Any())
            {
                failures.AddRange(upsertResponse.Failures.Select(f =>
                {
                    var failedRequest = newDataHubEntities.First(s => s.DataHubEntityId == f.Item.Value<string>(nameof(DataHubEntity.id)));
                    return new MergeEntityResult()
                    {
                        DataSource = failedRequest.DataSource,
                        SourceEntityType = failedRequest.SourceEntityType,
                        SourceEntityId = failedRequest.SourceEntityId,
                        DataHubEntityType = failedRequest.DataHubEntityType,
                        MergeOutcome = MergeOutcomes.MergeFailed,
                        FailureReason = f.Error?.Message
                    };
                }));
            }

            successes.AddRange(newDataHubEntities.Where(entity => upsertResponse.Successes.Any(success => success.Value<string>(nameof(DataHubEntity.id)).Contains(entity.DataHubEntityId))));
        }
    }

    private async Task CommitUpdatedDataHubEntitiesToDb(CancellationToken cancellationToken, List<MergeEntityResult> successes, List<MergeEntityResult> updatedDataHubEntities)
    {
        if (updatedDataHubEntities.Any())
        {
            updatedDataHubEntities.ForEach(e => e.ResultingDataHubEntity[nameof(DataHubEntity.lastUpdated)] = timeService.Now());
            _ = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
            {
                Entities = updatedDataHubEntities.Select(s => s.ResultingDataHubEntity.RemoveNullValues()).ToList()
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            successes.AddRange(updatedDataHubEntities);
        }
    }


    #endregion
}
