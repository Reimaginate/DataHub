using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.MergeExistingUntrackedEntities;
using Reimaginate.DataHub.Requests.Internal.MergeNewEntities;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;


// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Reimaginate.DataHub.Requests.External.Client.MergeUntrackedEntities;

public class MergeUntrackedEntitiesRequestHandler(IMediator mediator, IEntityConfigService entityConfigService, IProcessingLockService processingLockService)
    : IHandler<MergeUntrackedEntitiesRequest, MergeEntitiesResponse>
{
    private List<ProcessingLock> _sourceSystemEntityLocks;

    public async Task<MergeEntitiesResponse> HandleAsync(MergeUntrackedEntitiesRequest request, CancellationToken cancellationToken)
    {

        var (results, notifications) = InitializeVars();

        var sourceEntityLockIds = request.Requests.Select(s => $"entities/{s.DataSource}/{s.SourceEntityType}/{s.SourceEntityId}").ToList();

        try
        {
            var getLocksResponse = await processingLockService.WaitForLocksAsync(sourceEntityLockIds, request.CorrelationId, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5),  cancellationToken: cancellationToken);
            getLocksResponse.ThrowIfUnsuccessful();
            _sourceSystemEntityLocks = getLocksResponse.Result;

            var entityTypeGroup = request.Requests.GroupBy(g => new { g.DataSource, g.SourceEntityType, g.DataHubEntityType });

            foreach (var group in entityTypeGroup)
            {
                var mergeRequests = request.Requests
                    .Where(w => w.DataSource == group.Key.DataSource
                                && w.SourceEntityType == group.Key.SourceEntityType
                                && w.DataHubEntityType == group.Key.DataHubEntityType)
                    .ToList();

                var entityConfig = await entityConfigService.GetEntityConfig(group.Key.DataHubEntityType, cancellationToken);
                var resolvedDataHubEntities = await ResolveDataHubEntities(request, cancellationToken, mergeRequests, results, group.Key.DataSource, group.Key.DataHubEntityType, group.Key.SourceEntityType);

                var (newEntityIds, existingEntityIds) = CalculateNewAndExistingEntityIds(resolvedDataHubEntities, mergeRequests);

                var newEntityMergeRequestsToProcessAsUpdates = new List<MergeEntityRequest>();

                await ProcessNewEntities(cancellationToken, newEntityIds, mergeRequests, newEntityMergeRequestsToProcessAsUpdates, group.Key.DataSource, group.Key.DataHubEntityType, group.Key.SourceEntityType, resolvedDataHubEntities, entityConfig, results);
                await ProcessExistingEntities(cancellationToken, existingEntityIds, newEntityMergeRequestsToProcessAsUpdates, mergeRequests, group.Key.DataSource, group.Key.DataHubEntityType, group.Key.SourceEntityType, resolvedDataHubEntities, entityConfig, results, request.CorrelationId);
            }

            HandleSuccessesAndFailures(results, notifications);

            return new MergeEntitiesResponse()
            {
                Success = true,
                Results = results
            };
        }
        catch (Exception ex)
        {
            return new MergeEntitiesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
        finally
        {
            if (_sourceSystemEntityLocks != null) await processingLockService.ReleaseLocksAsync(_sourceSystemEntityLocks, cancellationToken);

            if (notifications.Any())
            {
                _ = (await mediator.SendAsync(new DispatchNotificationsRequest()
                {
                    Notifications = notifications
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
            }
        }
    }


    #region Private helpers

    private static (List<string> newEntityIds, List<string> existingEntityIds) CalculateNewAndExistingEntityIds(IEnumerable<ResolvedEntityReference> resolvedDataHubEntities, IReadOnlyCollection<MergeEntityRequest> mergeRequests)
    {
        var resolvedEntitySourceSystemIds = resolvedDataHubEntities.Select(s => s.SourceEntityReference.EntityId).ToList();
        var newEntityIds = mergeRequests.Select(s => s.SourceEntityId).Except(resolvedEntitySourceSystemIds).ToList();
        var existingEntityIds = mergeRequests.Select(s => s.SourceEntityId).Intersect(resolvedEntitySourceSystemIds).ToList();
        return (newEntityIds, existingEntityIds);
    }

    private static void HandleSuccessesAndFailures(IReadOnlyCollection<MergeEntityResult> results, List<Notification> notifications)
    {
        var failures = results.Where(w => MergeOutcomes.IsFailure(w.MergeOutcome)).ToList();
        var successes = results.Except(failures).ToList();

        if (successes.Any())
        {
            notifications.AddRange(successes.Where(w => w.MergeOutcome == MergeOutcomes.NewEntityCreated).Select(s => new DataHubEntityCreatedNotification()
            {
                DataHubEntityId = s.DataHubEntityId,
                DataHubEntityType = s.DataHubEntityType,
                DataSource = s.DataSource,
                SourceEntityId = s.SourceEntityId,
                SourceEntityType = s.SourceEntityType,
                SourceEventTimeStamp = s.ResultingDataHubEntity.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated))
            }));

            notifications.AddRange(successes.Where(w => w.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated).Select(s => new DataHubEntityUpdatedNotification()
            {
                DataHubEntityId = s.DataHubEntityId,
                DataHubEntityType = s.DataHubEntityType,
                DataSource = s.DataSource,
                SourceEntityId = s.SourceEntityId,
                SourceEntityType = s.SourceEntityType,
                UpdatePaths = s.ResultingDataHubEntityUpdates?.Properties().Select(prop => prop.Name).ToList(),
                SourceEventTimeStamp = s.ResultingDataHubEntity.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated))
            }));
        }
    }

    private static (List<MergeEntityResult> results, List<Notification> notifications) InitializeVars()
    {
        var results = new List<MergeEntityResult>();
        var notifications = new List<Notification>();
        return (results, notifications);
    }

    private async Task ProcessExistingEntities(CancellationToken cancellationToken, ICollection<string> existingEntityIds, IReadOnlyCollection<MergeEntityRequest> newEntityMergeRequestsToProcessAsUpdates, IEnumerable<MergeEntityRequest> mergeRequests, string dataSource, string dataHubEntityType, string sourceEntityType, List<ResolvedEntityReference> resolvedDataHubEntities, EntityConfig entityConfig, List<MergeEntityResult> results, string correlationId)
    {
        if (existingEntityIds.Any() || newEntityMergeRequestsToProcessAsUpdates.Any())
        {
            List<ProcessingLock> dataHubEntityLocks = null;

            try
            {
                var lockIds = existingEntityIds.Select(entityId => $"entities/{DataSources.DataHub}/{dataHubEntityType}/{entityId}").ToList();
                var getLocksResponse = await processingLockService.WaitForLocksAsync(lockIds, correlationId, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
                getLocksResponse.ThrowIfUnsuccessful();
                dataHubEntityLocks = getLocksResponse.Result;

                var existingEntityMergeReqests = mergeRequests.Where(w => existingEntityIds.Contains(w.SourceEntityId)).ToList();
                var mergeRequestsToProcess = newEntityMergeRequestsToProcessAsUpdates.Concat(existingEntityMergeReqests).ToList();

                var mergeUpdatedEntitiesResponse = (await mediator.TrySend(new MergeExistingUntrackedEntitiesRequest()
                {
                    DataSource = dataSource,
                    DataHubEntityType = dataHubEntityType,
                    SourceEntityType = sourceEntityType,
                    MergeRequests = mergeRequestsToProcess,
                    ResolvedDataHubEntities = resolvedDataHubEntities,
                    EntityConfig = entityConfig
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                results.AddRange(mergeUpdatedEntitiesResponse.Results);
            }
            finally
            {
                if (dataHubEntityLocks != null) await processingLockService.ReleaseLocksAsync(dataHubEntityLocks, cancellationToken);
            }
        }
    }

    private async Task ProcessNewEntities(CancellationToken cancellationToken, List<string> newEntityIds, List<MergeEntityRequest> mergeRequests, List<MergeEntityRequest> newEntityMergeRequestsToProcessAsUpdates, string dataSource, string dataHubEntityType, string sourceEntityType, List<ResolvedEntityReference> resolvedDataHubEntities, EntityConfig entityConfig, List<MergeEntityResult> results)
    {
        if (newEntityIds.Any())
        {
            var newEntityMergeRequests = mergeRequests.Where(w => newEntityIds.Contains(w.SourceEntityId)).ToList();
            var groupedMergeRequests = newEntityMergeRequests.GroupBy(g => g.SourceEntityId);
            var mergeRequestsToProcess = groupedMergeRequests.Select(s => s.First()).ToList();

            var repeatedRequests = newEntityMergeRequests.Except(mergeRequestsToProcess).ToList();
            newEntityMergeRequestsToProcessAsUpdates.AddRange(repeatedRequests);

            var mergeNewEntitesResponse = (await mediator.TrySend(new MergeNewEntitiesRequest()
            {
                DataSource = dataSource,
                SourceEntityType = sourceEntityType,
                DataHubEntityType = dataHubEntityType,
                MergeRequests = mergeRequestsToProcess,
                ResolvedDataHubEntities = resolvedDataHubEntities,
                EntityConfig = entityConfig,
                DoNotTrack = true
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            results.AddRange(mergeNewEntitesResponse.Successes);
            results.AddRange(mergeNewEntitesResponse.Failures);

            mergeNewEntitesResponse.Successes
                .Where(e => e.MergeOutcome != MergeOutcomes.MergeSilentlyRejected)
                .ToList()
                .ForEach(e =>
            {
                if (resolvedDataHubEntities.All(a => a.SourceEntityReference.EntityId != e.SourceEntityId))
                {
                    resolvedDataHubEntities.Add(new ResolvedEntityReference()
                    {
                        DataHubEntityReference = new EntityReference()
                        {
                            EntityType = e.DataHubEntityType,
                            EntityId = e.SourceEntityId
                        },
                        SourceEntityReference = new ExternalEntityReference()
                        {
                            DataSource = e.DataSource,
                            EntityType = e.DataHubEntityType,
                            SourceEntityType = e.SourceEntityType,
                            EntityId = e.SourceEntityId
                        }
                    });
                }
            });
        }
    }

    private async Task<List<ResolvedEntityReference>> ResolveDataHubEntities(MergeUntrackedEntitiesRequest request, CancellationToken cancellationToken, List<MergeEntityRequest> mergeRequests, List<MergeEntityResult> results, string dataSource, string dataHubEntityType, string sourceEntityType)
    {
        var refs = mergeRequests.Select(s => new ExternalEntityReference()
        {
            DataSource = s.DataSource,
            SourceEntityType = s.SourceEntityType,
            EntityType = s.DataHubEntityType,
            EntityId = s.SourceEntityId
        }).ToList();

        var resolveDataHubEntitiesResponse = (await mediator.TrySend(new ResolveEntityReferencesRequest()
        {
            EntityReferences = refs
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (resolveDataHubEntitiesResponse.ResolutionFailures.Any())
        {
            results.AddRange(resolveDataHubEntitiesResponse.ResolutionFailures.Select(s => new MergeEntityResult()
            {
                DataSource = dataSource,
                DataHubEntityType = dataHubEntityType,
                SourceEntityType = dataHubEntityType,
                SourceEntityId = s.UnresolvedEntityReference.EntityId,
                MergeOutcome = MergeOutcomes.MergeFailed,
                FailureReason = s.Message
            }));

            var failedSourceEntityIds = resolveDataHubEntitiesResponse
                .ResolutionFailures
                .Select(s => s.UnresolvedEntityReference.EntityId)
                .ToList();

            var failedMergeRequests = mergeRequests
                .Where(w => failedSourceEntityIds.Contains(w.SourceEntityId)).ToList();

            failedMergeRequests.ForEach(failedMergeRequest => mergeRequests.Remove(failedMergeRequest));
        }

        var resolvedDataHubEntities = resolveDataHubEntitiesResponse.Results;
        return resolvedDataHubEntities;
    }

    #endregion
}
