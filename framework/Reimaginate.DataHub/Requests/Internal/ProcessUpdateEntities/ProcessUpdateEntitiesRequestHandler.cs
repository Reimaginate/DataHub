using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateEntities;

public class ProcessUpdateEntitiesRequestHandler(IMediator mediator, IProcessingLockService processingLockService, ITimeService timeService)
    : IHandler<ProcessUpdateEntitiesRequest, ProcessUpdateEntitiesResponse>
{
    public async Task<ProcessUpdateEntitiesResponse> HandleAsync(ProcessUpdateEntitiesRequest request, CancellationToken cancellationToken)
    {
        var dataHubEntityRequests = request.Requests.Where(w => w.DataSource == DataSources.DataHub).ToList();
        var srcEntityRequests = request.Requests.Where(w => w.DataSource != DataSources.DataHub).ToList();

        var ret = new ProcessUpdateEntitiesResponse()
        {
            Results = new List<ProcessUpdateEntityResponse>()
        };

        if (dataHubEntityRequests.Any())
        {
            var dataHubEntityResults = await ProcessDataHubEntityUpdates(dataHubEntityRequests, cancellationToken);
            ret.Results.AddRange(dataHubEntityResults);
        }

        if (srcEntityRequests.Any())
        {
            var srcEntityResults = await ProcessSrcEntityUpdates(srcEntityRequests, cancellationToken);
            ret.Results.AddRange(srcEntityResults);
        }

        return ret;
    }


    private async Task<List<ProcessUpdateEntityResponse>> ProcessSrcEntityUpdates(List<ProcessUpdateEntityRequest> requests, CancellationToken cancellationToken)
    {
        var results = requests.Select(s => new ProcessUpdateEntityResponse()
        {
            Success = true,
            EntityType = s.EntityType,
            EntityId = s.EntityId,
            ResultingEntity = s.Data,
            DataSource = s.DataSource,
        }).ToDictionary(k => $"{k.EntityType}:{k.EntityId}".ToLower(), v => v);

        var entityTypeGroups = requests.GroupBy(r => new { r.DataSource, r.EntityType });
        foreach (var entityTypeGroup in entityTypeGroups)
        {
            var srcEntityIds = entityTypeGroup.Select(s => s.EntityId).ToList();
            var failedEntityIds = new List<string>();

            var getTrackedEntitiesResponse = (await mediator.TrySend(new GetTrackedEntitiesRequest()
            {
                DataSource = entityTypeGroup.Key.DataSource,
                EntityType = entityTypeGroup.Key.EntityType,
                EntityIds = srcEntityIds
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var failures = getTrackedEntitiesResponse.Results.Where(w => w is not { Success: true }).ToList();
            if (failures.Any())
            {
                failures.ForEach(f =>
                {
                    failedEntityIds.Add(f.EntityId);
                    var resultKey = $"{f.EntityType}:{f.EntityId}".ToLower();
                    var result = results[resultKey];
                    result.Success = false;
                    result.Error = f.FailureReason;
                });
            }

            var trackedEntities = getTrackedEntitiesResponse.Results.Where(w => w is { Success: true } && w.Data != null).ToList();

            var existingEntityIds = trackedEntities.Select(s => s.EntityId).ToList();
            var newEntityIds = srcEntityIds.Except(failedEntityIds).Except(existingEntityIds).ToList();

            var initRequests = newEntityIds.SelectMany(id =>
            {
                return entityTypeGroup.Where(w => w.EntityId == id).Select(updateRequest => new InitTrackedEntityRequest()
                {
                    DataSource = updateRequest.DataSource,
                    EntityType = updateRequest.EntityType,
                    EntityId = updateRequest.EntityId,
                    Timestamp = updateRequest.Timestamp ?? timeService.Now(),
                    EntityData = updateRequest.Data
                });
            }).ToList();

            if (initRequests.Any())
            {
                var initTrackedEntityResponse = (await mediator.TrySend(new InitTrackedEntitiesRequest()
                {
                    Requests = initRequests
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                initTrackedEntityResponse.Failures.ForEach(f =>
                {
                    var result = results[$"{f.Item.EntityType}:{f.Item.EntityId}".ToLower()];
                    result.Success = false;
                    result.Error = f.Error.Message;
                });
            }

            var addChangeSetRequests = existingEntityIds.SelectMany(id =>
            {
                return entityTypeGroup.Where(w => w.EntityId == id).Select(updateRequest => new AddTrackedEntityChangeSetRequest()
                {
                    DataSource = updateRequest.DataSource,
                    EntityType = updateRequest.EntityType,
                    SourceEntityId = updateRequest.EntityId,
                    TimeStamp = updateRequest.Timestamp ?? timeService.Now(),
                    ChangeSet = updateRequest.Data
                });
            }).ToList();

            if (addChangeSetRequests.Any())
            {
                var addTrackedEntityUpdateResponse = (await mediator.TrySend(new AddTrackedEntityChangeSetsRequest()
                {
                    Requests = addChangeSetRequests
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                addTrackedEntityUpdateResponse.Failures.ForEach(f =>
                {
                    var result = results[$"{f.Item.EntityType}:{f.Item.EntityId}".ToLower()];
                    result.Success = false;
                    result.Error = f.Error.Message;
                });
            }
        }

        return results.Values.ToList();
    }

    private async Task<List<ProcessUpdateEntityResponse>> ProcessDataHubEntityUpdates(List<ProcessUpdateEntityRequest> requests, CancellationToken cancellationToken)
    {
        var responses = new List<ProcessUpdateEntityResponse>();

        var entityLockIds = requests.Select(s => $"entities/{DataSources.DataHub}/{s.EntityType}/{s.EntityId}").ToList();
        List<ProcessingLock> dataHubEntityLocks = null;
        try
        {
            var getLocksResponse = await processingLockService.WaitForLocksAsync(entityLockIds, null, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            getLocksResponse.ThrowIfUnsuccessful();
            dataHubEntityLocks = getLocksResponse.Result;

            var requestsGroupedByEntityType = requests.GroupBy(g => new { g.EntityType });
            foreach (var entityTypeGroup in requestsGroupedByEntityType)
            {
                var upsertRequests = entityTypeGroup.ToList();

                var getAllTrackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest()
                {
                    DataSource = DataSources.DataHub,
                    EntityType = entityTypeGroup.Key.EntityType,
                    EntityIds = upsertRequests.Select(s => s.EntityId).ToList()
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                var currentTrackingEntries = getAllTrackingEntriesResponse.TrackingEntries;

                var entityIdsToProcess = new List<string>(upsertRequests.Select(s => s.EntityId).Distinct());
                var getDataHubEntitiesByIdResponse = (await mediator.TrySend(new GetDataHubEntitiesByIdRequest()
                {
                    EntityType = entityTypeGroup.Key.EntityType,
                    EntityIds = entityIdsToProcess
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                var existingEntities = getDataHubEntitiesByIdResponse.Results;
                var existingEntityIds = existingEntities.Select(s => s.DataHubEntityId()).ToList();
                var newEntities = upsertRequests.Select(s => s.Data).Where(w => !existingEntityIds.Contains(w.DataHubEntityId())).ToList();

                var entitiesToProcess = existingEntities.Union(newEntities).ToList();
                var originalEntities = entitiesToProcess.Select(s => s.DeepClone()).ToList();

                var entitiesToCreate = new List<(ProcessUpdateEntityRequest, JObject)>();
                var entitiesToUpdate = new List<(ProcessUpdateEntityRequest, JObject, AddTrackedEntityChangeSetRequest)>();

                foreach (var entityToProcess in entitiesToProcess)
                {
                    var entityId = entityToProcess.DataHubEntityId();
                    var entityType = entityTypeGroup.Key.EntityType;
                    var upsertRequestsForEntity = requests.Where(w => w.EntityType == entityType && w.EntityId == entityId).ToList();

                    foreach (var upsertEntityRequest in upsertRequestsForEntity)
                    {
                        var currentChangeTrackingForEntity = currentTrackingEntries.Where(w => w.EntityId == entityId);

                        if (!currentChangeTrackingForEntity.Any())
                        {
                            if (upsertEntityRequest.CreateOnly || !upsertEntityRequest.UpdateOnly)
                            {
                                entityToProcess[nameof(DataHubEntity.lastUpdated)] = upsertEntityRequest.Timestamp ?? entityToProcess.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated)) ?? timeService.Now();
                                entitiesToCreate.Add((upsertEntityRequest, entityToProcess));
                                continue;
                            }

                            responses.Add(new ProcessUpdateEntityResponse()
                            {
                                EntityType = upsertEntityRequest.EntityType,
                                DataSource = upsertEntityRequest.DataSource,
                                EntityId = upsertEntityRequest.EntityId,
                                Success = false,
                                Error = "Entity does not exist"
                            });

                            continue;
                        }
                        else
                        {
                            if (upsertEntityRequest.CreateOnly)
                            {
                                responses.Add(new ProcessUpdateEntityResponse()
                                {
                                    EntityType = upsertEntityRequest.EntityType,
                                    DataSource = upsertEntityRequest.DataSource,
                                    EntityId = upsertEntityRequest.EntityId,
                                    Success = false,
                                    Error = "Entity already exists"
                                });

                                continue;
                            }
                        }

                        var targetState = entityToProcess;
                        var currentState = (JObject)entityToProcess.DeepClone();

                        targetState.Merge(upsertEntityRequest.Data, new JsonMergeSettings()
                        {
                            MergeNullValueHandling = upsertEntityRequest.UpdateType?.ToLower() == "patch" ? MergeNullValueHandling.Ignore : MergeNullValueHandling.Merge,
                            MergeArrayHandling = MergeArrayHandling.Replace
                        });

                        var entityDiffs = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(currentState, targetState));
                        if (entityDiffs?.HasValues == true)
                        {
                            targetState[nameof(DataHubEntity.lastUpdated)] = timeService.Now();

                            var addChangeSetRequest = new AddTrackedEntityChangeSetRequest
                            {
                                DataSource = DataSources.DataHub,
                                EntityType = entityTypeGroup.Key.EntityType,
                                SourceEntityId = entityToProcess.DataHubEntityId(),
                                ChangeSet = entityDiffs,
                                TimeStamp = timeService.Now()
                            };

                            entitiesToUpdate.Add((upsertEntityRequest, entityToProcess, addChangeSetRequest));
                        }
                    }
                }

                if (entitiesToCreate.Any())
                {
                    var initTrackedEntitiesResponse = (await mediator.TrySend(new InitTrackedEntitiesRequest()
                    {
                        Requests = entitiesToCreate.Select(s => new InitTrackedEntityRequest()
                        {
                            DataSource = DataSources.DataHub,
                            EntityType = s.Item1.EntityType,
                            EntityId = s.Item1.EntityId,
                            EntityData = s.Item2,
                            Timestamp = s.Item2.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated))
                        }
                        ).ToList()
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };


                    var upsertDataHubEntitiesResponse = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
                    {
                        Entities = entitiesToCreate.Select(s => s.Item2.RemoveNullValues()).ToList()
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };


                    responses.AddRange(upsertDataHubEntitiesResponse.Failures.Select(s => new ProcessUpdateEntityResponse()
                    {
                        EntityType = entityTypeGroup.Key.EntityType,
                        DataSource = DataSources.DataHub,
                        EntityId = s.Item.DataHubEntityId(),
                        Error = s.Error?.Message,
                        Success = false
                    }));

                    responses.AddRange(upsertDataHubEntitiesResponse.Successes.Select(s => new ProcessUpdateEntityResponse()
                    {
                        EntityType = entityTypeGroup.Key.EntityType,
                        DataSource = DataSources.DataHub,
                        EntityId = s.DataHubEntityId(),
                        ResultingEntity = s,
                        Success = true
                    }));
                }

                if (entitiesToUpdate.Any())
                {
                    var addChangeSetsResponse = (await mediator.TrySend(new AddTrackedEntityChangeSetsRequest()
                    {
                        Requests = entitiesToUpdate.Select(s => s.Item3).ToList()
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    if (addChangeSetsResponse.Failures.Any())
                    {
                        responses.AddRange(addChangeSetsResponse.Failures.Select(s => new ProcessUpdateEntityResponse()
                        {
                            EntityType = entityTypeGroup.Key.EntityType,
                            DataSource = DataSources.DataHub,
                            EntityId = s.Item.EntityId,
                            Error = $"Failed to add change tracking entries: {s.Error}",
                            Success = false
                        }));
                    }

                    var upsertDataHubEntitiesResponse = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
                    {
                        Entities = entitiesToUpdate.Select(s => s.Item2.RemoveNullValues()).ToList()
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    responses.AddRange(upsertDataHubEntitiesResponse.Failures.Select(s => new ProcessUpdateEntityResponse()
                    {
                        EntityType = entityTypeGroup.Key.EntityType,
                        DataSource = DataSources.DataHub,
                        EntityId = s.Item.DataHubEntityId(),
                        Error = s.Error?.Message,
                        Success = false
                    }));

                    responses.AddRange(upsertDataHubEntitiesResponse.Successes.Select(s => new ProcessUpdateEntityResponse()
                    {
                        EntityType = entityTypeGroup.Key.EntityType,
                        DataSource = DataSources.DataHub,
                        EntityId = s.DataHubEntityId(),
                        ResultingEntity = s,
                        Success = true
                    }));
                }

                #region Dispatch notification to agents

                var entityCreatedNotifications = entitiesToCreate.Select(createdEntity =>
                {
                    var dataHubEntityId = createdEntity.Item2[nameof(DataHubEntity.id)].Value<string>()!;
                    var dataHubEntityType = createdEntity.Item2[nameof(DataHubEntity.entityType)].Value<string>()!;

                    return (Notification)new DataHubEntityCreatedNotification()
                    {
                        DataHubEntityId = dataHubEntityId,
                        DataHubEntityType = dataHubEntityType,
                        DataSource = DataSources.DataHub
                    };

                }).ToList();

                var entityUpdatedNotifications = entitiesToUpdate.Select(updatedEntity =>
                {
                    var dataHubEntityId = updatedEntity.Item2[nameof(DataHubEntity.id)].Value<string>()!;
                    var dataHubEntityType = updatedEntity.Item2[nameof(DataHubEntity.entityType)].Value<string>()!;

                    var originalEntity = originalEntities.First(f => f.DataHubEntityId() == dataHubEntityId);
                    var entityDiffs = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(originalEntity, updatedEntity.Item2));
                    
                    return (Notification)new DataHubEntityUpdatedNotification()
                    {
                        DataHubEntityId = dataHubEntityId,
                        DataHubEntityType = dataHubEntityType,
                        DataSource = DataSources.DataHub,
                        UpdatePaths = entityDiffs?.Properties().Select(s => s.Name).ToList()
                    };
                }).ToList();

                var notifications = entityCreatedNotifications.Concat(entityUpdatedNotifications).ToList();

                if (notifications.Any())
                {
                    _ = (await mediator.SendAsync(new DispatchNotificationsRequest()
                    {
                        Notifications = notifications
                    }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
                }

                #endregion
            }

            return responses;
        }
        finally
        {
            if (dataHubEntityLocks != null) await processingLockService.ReleaseLocksAsync(dataHubEntityLocks, cancellationToken);
        }
    }
}
