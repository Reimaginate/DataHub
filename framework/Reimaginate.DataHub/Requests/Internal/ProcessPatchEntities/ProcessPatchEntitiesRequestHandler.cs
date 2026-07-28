using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;

public class ProcessPatchEntitiesRequestHandler(IMediator mediator, IProcessingLockService processingLockService, ITimeService timeService)
    : IHandler<ProcessPatchEntitiesRequest, ProcessPatchEntitiesResponse>
{
    public async Task<ProcessPatchEntitiesResponse> HandleAsync(ProcessPatchEntitiesRequest request, CancellationToken cancellationToken)
    {
        var results = new List<ProcessPatchEntityResponse>();

        var groupedByEntityType = request.Requests.GroupBy(g => new { g.DataSource, g.EntityType });
        foreach (var entityTypeGroup in groupedByEntityType)
        {
            var requestsToProcess = new List<ProcessPatchEntityRequest>(entityTypeGroup.ToList());

            while (requestsToProcess.Any())
            {
                var batch = requestsToProcess.Take(5000).ToList();
                
                var entityLockIds = batch.Select(r => $"entities/{r.DataSource}/{r.EntityType}/{r.EntityId}").ToList();
                List<ProcessingLock> dataHubEntityLocks = null;

                try
                {
                    var getLocksResponse = await processingLockService.WaitForLocksAsync(entityLockIds, request.CorrelationId, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
                    getLocksResponse.ThrowIfUnsuccessful();
                    dataHubEntityLocks = getLocksResponse.Result;

                    var batchEntityIds = batch.Select(s => s.EntityId).ToList();
                    var batchDataHubEntityIds = batch.Where(w => w.DataSource == DataSources.DataHub).Select(s => s.EntityId).ToList();

                    var trackingEntriesTask = mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest()
                    {
                        DataSource = entityTypeGroup.Key.DataSource,
                        EntityType = entityTypeGroup.Key.EntityType,
                        EntityIds = batchEntityIds
                    }, cancellationToken);

                    Task<GetMaterializedEntitiesByIdResponse> dataHubEntitiesTask = batchDataHubEntityIds.Any()
                        ? GetMaterializedEntitiesAsync(new GetMaterializedEntitiesByIdRequest()
                        {
                            CorrelationId = request.CorrelationId,
                            EntityType = entityTypeGroup.Key.EntityType,
                            EntityIds = batchDataHubEntityIds
                        }, cancellationToken)
                        : Task.FromResult<GetMaterializedEntitiesByIdResponse>(null);

                    await Task.WhenAll(trackingEntriesTask, dataHubEntitiesTask);

                    var trackingEntriesResponse = await trackingEntriesTask;
                    if (trackingEntriesResponse.Exception != null) throw trackingEntriesResponse.Exception;

                    var trackingEntries = trackingEntriesResponse.Response.TrackingEntries;
                    var dataHubEntities = (await dataHubEntitiesTask)?.Results;

                    var tasks = new List<Task<ProcessPatchEntityResponse>>();

                    foreach (var r in batch)
                    {
                        r.CorrelationId = request.CorrelationId;
                        r.CommitToDb = false;
                        r.Silent = request.Silent || r.Silent;
                        r.DoNotTrack = request.DoNotTrack || r.DoNotTrack;
                        r.DispatchNotifications = request.DispatchNotifications || r.DispatchNotifications;
                        r.Cache = new Dictionary<string, object>()
                        {
                            { "TrackingEntries", trackingEntries }
                        };

                        if (r.DataSource == DataSources.DataHub)
                        {
                            r.Cache.Add("DataHubEntities", dataHubEntities);
                        }

                        tasks.Add(Task.Run(async () => (await mediator.TrySend(r, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue }, cancellationToken));
                    }

                    await Task.WhenAll(tasks);

                    var patchEntityResponses = tasks.Select(s => s.Result).ToList();

                    var addTrackingRequests = patchEntityResponses.Where(w => !request.DoNotTrack && w.IsTrackedEntity && w.ChangeSet != null).Select(response =>
                    {
                        var req = batch[patchEntityResponses.IndexOf(response)];

                        return new AddTrackedEntityChangeSetRequest()
                        {
                            DataSource = entityTypeGroup.Key.DataSource,
                            EntityType = entityTypeGroup.Key.EntityType,
                            SourceEntityId = req.EntityId,
                            ChangeSet = response.ChangeSet,
                            TimeStamp = req.Timestamp ?? timeService.Now()
                        };
                    }).ToList();

                    if (addTrackingRequests.Any())
                    {
                        var addChangeSetsResponse = (await mediator.SendAsync(new AddTrackedEntityChangeSetsRequest()
                        {
                            Requests = addTrackingRequests
                        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

                        foreach (var failure in addChangeSetsResponse.Failures)
                        {
                            var failedResponse = patchEntityResponses.FirstOrDefault(response =>
                            {
                                var req = batch[patchEntityResponses.IndexOf(response)];
                                return req.DataSource == failure.Item.DataSource &&
                                       req.EntityType == failure.Item.EntityType &&
                                       req.EntityId == failure.Item.EntityId;
                            });

                            if (failedResponse != null)
                            {
                                MarkResponseFailed(failedResponse, BuildPersistenceFailureReason("Failed to add change tracking entry", [failure.Error?.Message]));
                            }
                        }
                    }

                    var updatedEntityResponses = patchEntityResponses.Where(w => w.IsDataHubEntity && w.ChangeSet != null && w.UpdatedEntity != null).ToList();
                    if (updatedEntityResponses.Any())
                    {
                        var upsertDataHubEntitiesResponse = (await mediator.SendAsync(new UpsertDataHubEntitiesCommand()
                        {
                            Entities = updatedEntityResponses.Select(s => s.UpdatedEntity.RemoveNullValues()).ToList()
                        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

                        foreach (var failure in upsertDataHubEntitiesResponse.Failures)
                        {
                            var failedEntityId = failure.Item.DataHubEntityId();
                            var failedResponse = updatedEntityResponses.FirstOrDefault(response => response.UpdatedEntity.DataHubEntityId() == failedEntityId);

                            if (failedResponse != null)
                            {
                                MarkResponseFailed(failedResponse, BuildPersistenceFailureReason("Failed to upsert DataHub entity", [failure.Error?.Message]));
                            }
                        }
                    }

                    var notifications = patchEntityResponses.Where(w => w.Success && w.Notify).Select(response => (Notification)new DataHubEntityUpdatedNotification()
                    {
                        DataSource = DataSources.DataHub,
                        DataHubEntityType = response.UpdatedEntity.Value<string>(nameof(DataHubEntity.entityType)),
                        DataHubEntityId = response.UpdatedEntity.Value<string>(nameof(DataHubEntity.id)),
                        UpdatePaths = response.ChangeSet?.Properties().Select(s=>s.Name).ToList()
                    }).ToList();

                    if (notifications.Any())
                    {
                        _ = (await mediator.SendAsync(new DispatchNotificationsRequest()
                        {
                            Notifications = notifications
                        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
                    }

                    results.AddRange(patchEntityResponses);
                }
                finally
                {
                    if (dataHubEntityLocks != null) await processingLockService.ReleaseLocksAsync(dataHubEntityLocks, cancellationToken);
                }

                requestsToProcess.RemoveRange(0, batch.Count);
            }
        }

        return new ProcessPatchEntitiesResponse()
        {
            Results = results
        };
    }

    private async Task<GetMaterializedEntitiesByIdResponse> GetMaterializedEntitiesAsync(GetMaterializedEntitiesByIdRequest request, CancellationToken cancellationToken)
    {
        return (await mediator.SendAsync(request, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
    }

    private static void MarkResponseFailed(ProcessPatchEntityResponse response, string failureReason)
    {
        response.Success = false;
        response.FailureReason = string.IsNullOrWhiteSpace(response.FailureReason)
            ? failureReason
            : $"{response.FailureReason}; {failureReason}";
        response.Notify = false;
    }

    private static string BuildPersistenceFailureReason(string prefix, IEnumerable<string> errors)
    {
        var errorText = string.Join("; ", errors.Where(w => !string.IsNullOrWhiteSpace(w)));
        return string.IsNullOrWhiteSpace(errorText) ? prefix : $"{prefix}: {errorText}";
    }
}
