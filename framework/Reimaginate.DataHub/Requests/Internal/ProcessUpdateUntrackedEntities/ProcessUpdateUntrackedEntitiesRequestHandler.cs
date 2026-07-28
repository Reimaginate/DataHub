using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CreateDataHubEntities;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateUntrackedEntities;

public class ProcessUpdateUntrackedEntitiesRequestHandler(IMediator mediator, IProcessingLockService processingLockService, ITimeService timeService)
    : IHandler<ProcessUpdateUntrackedEntitiesRequest, ProcessUpdateUntrackedEntitiesResponse>
{
    public async Task<ProcessUpdateUntrackedEntitiesResponse> HandleAsync(ProcessUpdateUntrackedEntitiesRequest request, CancellationToken cancellationToken)
    {
        var entityLockIds = request.Requests.Select(s => $"entities/{DataSources.DataHub}/{s.EntityType}/{s.EntityId}").ToList();
        List<ProcessingLock> dataHubEntityLocks = null;

        try
        {
            var getLocksResponse = await processingLockService.WaitForLocksAsync(entityLockIds, null, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            getLocksResponse.ThrowIfUnsuccessful();
            dataHubEntityLocks = getLocksResponse.Result;

            var now = timeService.Now();
            var results = new List<ProcessUpdateUntrackedEntityResponse>();
            var notifications = new List<Notification>();
            var normalizedRequests = request.Requests.Select(NormalizeRequest).ToList();

            var entityTypeGroups = normalizedRequests.GroupBy(g => g.EntityType);
            foreach (var entityTypeGroup in entityTypeGroups)
            {
                var entityTypeRequests = entityTypeGroup.ToList();
                var entityTypeRequestsDict = entityTypeRequests.ToDictionary(k => k.EntityId, v => v);

                var entityType = entityTypeGroup.Key;
                var entityIds = entityTypeRequests.Select(s => s.EntityId).ToList();

                var existingEntityIds = new List<string>();
                var existingEntityIdTimestamps = new Dictionary<string, DateTimeOffset>();

                var entityIdsToProcess = new List<string>(entityIds);
                while (entityIdsToProcess.Count != 0)
                {
                    var batch = entityIdsToProcess.Take(500).ToList();
                    var parameters = new List<QueryParameter>
                    {
                        new("entityType", entityType)
                    };
                    var entityIdParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(batch, "entityId", parameters);

                    var query = new GetDataHubEntitiesQuery()
                    {
                        Select = "x.id,x.lastUpdated",
                        WhereClause = $"x.{nameof(DataHubEntity.entityType)} = @entityType and x.{nameof(DataHubEntity.id)} in ({string.Join(",", entityIdParameterNames)})",
                        OrderBy = $"x.{nameof(DataHubEntity.entityType)}",
                        PageSize = 500,
                        Parameters = parameters
                    };

                    var queryResponse = (await mediator.TrySend(query, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    existingEntityIds.AddRange(queryResponse.Results.Select(s => s.Value<string>(nameof(DataHubEntity.id))));
                    foreach (var result in queryResponse.Results)
                    {
                        var entityId = result.Value<string>(nameof(DataHubEntity.id));
                        var lastUpdatedTimestamp = result.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated));

                        existingEntityIdTimestamps.Add(entityId, lastUpdatedTimestamp);
                    }

                    entityIdsToProcess.RemoveRange(0, batch.Count);
                }

                var existingEntityUpdateRequests = new List<ProcessUpdateUntrackedEntityRequest>();
                var newEntityUpdateRequests = entityTypeRequests.Where(w => !existingEntityIds.Contains(w.EntityId)).ToList();
                var newEntityCreateRequests = newEntityUpdateRequests.Where(w => w.CreateIfMissing != false).ToList();
                var newEntityUpdateOnlyRequests = newEntityUpdateRequests.Where(w => w.CreateIfMissing == false).ToList();

                foreach (var existingEntityId in existingEntityIds)
                {
                    var importRequest = entityTypeRequestsDict[existingEntityId];
                    existingEntityUpdateRequests.Add(importRequest);
                }

                if (newEntityUpdateOnlyRequests.Any())
                {
                    results.AddRange(newEntityUpdateOnlyRequests.Select(updateRequest => new ProcessUpdateUntrackedEntityResponse()
                    {
                        EntityType = updateRequest.EntityType,
                        EntityId = updateRequest.EntityId,
                        Success = false,
                        FailureReason = "Entity does not exist"
                    }));
                }

                if (newEntityCreateRequests.Any())
                {
                    newEntityCreateRequests.ForEach(updateRequest =>
                    {
                        if (!updateRequest.Data.ContainsKey(nameof(DataHubEntity.createdOn)))
                            updateRequest.Data.Add(nameof(DataHubEntity.createdOn), now);

                        if (!updateRequest.Data.ContainsKey(nameof(DataHubEntity.lastUpdated)))
                            updateRequest.Data.Add(nameof(DataHubEntity.lastUpdated), now);
                    });

                    var entitiesToCreate = newEntityCreateRequests.Select(s => s.Data.RemoveNullValues()).ToList();
                    var createEntitiesResponse = (await mediator.TrySend(new CreateDataHubEntitiesCommand()
                    {
                        Entities = entitiesToCreate
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    var response = entitiesToCreate.Select(dataHubEntity =>
                    {
                        var entityId = dataHubEntity.Value<string>(nameof(DataHubEntity.id));
                        var failure = createEntitiesResponse.Failures.FirstOrDefault(f => f.Item.Value<string>(nameof(DataHubEntity.id)) == entityId);

                        return new ProcessUpdateUntrackedEntityResponse()
                        {
                            EntityType = entityType,
                            EntityId = entityId,
                            Success = failure == null,
                            FailureReason = failure?.Error.Message
                        };
                    }).ToList();

                    results.AddRange(response);
                    notifications.AddRange(response
                        .Where(w => w.Success)
                        .Select(s => entityTypeRequestsDict[s.EntityId])
                        .Where(s => ShouldDispatchNotifications(s, request))
                        .Select(s => (Notification)new DataHubEntityCreatedNotification()
                        {
                            DataSource = DataSources.DataHub,
                            DataHubEntityId = s.EntityId,
                            DataHubEntityType = s.EntityType,
                            SourceEventTimeStamp = s.Data.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated))
                        }));
                }

                if (existingEntityUpdateRequests.Any())
                {
                    existingEntityUpdateRequests.ForEach(updateRequest =>
                    {
                        if (!updateRequest.Data.ContainsKey(nameof(DataHubEntity.createdOn)))
                            updateRequest.Data.Add(nameof(DataHubEntity.createdOn), now);

                        if (!updateRequest.Data.ContainsKey(nameof(DataHubEntity.lastUpdated)))
                            updateRequest.Data.Add(nameof(DataHubEntity.lastUpdated), now);

                        if (request.Silent || updateRequest.Silent)
                        {
                            updateRequest.Data[nameof(DataHubEntity.lastUpdated)] = existingEntityIdTimestamps[updateRequest.EntityId];
                        }
                        else
                        {
                            updateRequest.Data[nameof(DataHubEntity.lastUpdated)] = now;
                        }
                    });

                    var untrackedEntities = existingEntityUpdateRequests.ToList();
                    if (untrackedEntities.Any())
                    {
                        var updateRequests = new UpsertDataHubEntitiesCommand()
                        {
                            Entities = untrackedEntities.Select(s => s.Data.RemoveNullValues()).ToList()
                        };

                        var updateResponse = (await mediator.TrySend(updateRequests, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                        var response = untrackedEntities.Select(dataHubEntity =>
                        {
                            var entityId = dataHubEntity.EntityId;
                            var failure = updateResponse.Failures.FirstOrDefault(f => f.Item.Value<string>(nameof(DataHubEntity.id)) == entityId);

                            return new ProcessUpdateUntrackedEntityResponse()
                            {
                                EntityType = entityType,
                                EntityId = entityId,
                                Success = failure == null,
                                FailureReason = failure?.Error.Message
                            };
                        }).ToList();

                        results.AddRange(response);
                        notifications.AddRange(response
                            .Where(w => w.Success)
                            .Select(s => entityTypeRequestsDict[s.EntityId])
                            .Where(s => ShouldDispatchNotifications(s, request))
                            .Select(s => (Notification)new DataHubEntityUpdatedNotification()
                            {
                                DataSource = DataSources.DataHub,
                                DataHubEntityId = s.EntityId,
                                DataHubEntityType = s.EntityType,
                                SourceEventTimeStamp = s.Data.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated)),
                                UpdatePaths = UpdatePaths(s.Data)
                            }));
                    }
                }
            }

            if (notifications.Any())
            {
                _ = (await mediator.SendAsync(new DispatchNotificationsRequest()
                {
                    Notifications = notifications
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
            }

            return new ProcessUpdateUntrackedEntitiesResponse()
            {
                Results = results
            };
        }
        finally
        {
            if (dataHubEntityLocks != null) await processingLockService.ReleaseLocksAsync(dataHubEntityLocks, cancellationToken);
        }
    }

    private static ProcessUpdateUntrackedEntityRequest NormalizeRequest(ProcessUpdateUntrackedEntityRequest request)
    {
        var data = (JObject)request.Data.DeepClone();
        data[nameof(DataHubEntity.id)] = request.EntityId;
        data[nameof(DataHubEntity.entityType)] = request.EntityType;

        return new ProcessUpdateUntrackedEntityRequest()
        {
            CreateIfMissing = request.CreateIfMissing,
            Data = data,
            DispatchNotifications = request.DispatchNotifications,
            EntityId = request.EntityId,
            EntityType = request.EntityType,
            Silent = request.Silent
        };
    }

    private static bool ShouldDispatchNotifications(ProcessUpdateUntrackedEntityRequest itemRequest, ProcessUpdateUntrackedEntitiesRequest batchRequest)
    {
        return batchRequest.DispatchNotifications || itemRequest.DispatchNotifications;
    }

    private static List<string> UpdatePaths(JObject data)
    {
        var excludedProperties = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase)
        {
            nameof(DataHubEntity.id),
            nameof(DataHubEntity.entityType),
            nameof(DataHubEntity.createdOn),
            nameof(DataHubEntity.lastUpdated),
            nameof(DataHubEntity._dt),
            nameof(DataHubEntity._ts),
            nameof(DataHubEntity.pk)
        };

        return data.Properties()
            .Select(s => s.Name)
            .Where(w => !excludedProperties.Contains(w))
            .ToList();
    }
}
