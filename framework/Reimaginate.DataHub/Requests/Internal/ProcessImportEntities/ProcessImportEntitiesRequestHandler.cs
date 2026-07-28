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
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessImportEntities;

public class ProcessImportEntitiesRequestHandler(IMediator mediator, IProcessingLockService processingLockService, ITimeService timeService)
    : IHandler<ProcessImportEntitiesRequest, List<ImportEntityResponse>>
{
    public async Task<List<ImportEntityResponse>> HandleAsync(ProcessImportEntitiesRequest request, CancellationToken cancellationToken)
    {
        var entityLockIds = request.ImportEntityRequests.Select(s => $"entities/{DataSources.DataHub}/{s.EntityType}/{s.EntityId}").ToList();
        List<ProcessingLock> dataHubEntityLocks = null;

        try
        {
            var getLocksResponse = await processingLockService.WaitForLocksAsync(entityLockIds, request.CorrelationId, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            getLocksResponse.ThrowIfUnsuccessful();
            dataHubEntityLocks = getLocksResponse.Result;

            var now = timeService.Now();
            var responses = new List<ImportEntityResponse>();

            var entityTypeGroups = request.ImportEntityRequests.GroupBy(g => g.EntityType);
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

                        entityIds.Add(entityId);
                        existingEntityIdTimestamps.Add(entityId, lastUpdatedTimestamp);
                    }

                    entityIdsToProcess.RemoveRange(0, batch.Count);
                }

                var existingEntityImportRequests = new List<ImportEntityRequest>();
                var newEntityImportRequests = entityTypeRequests.Where(w => !existingEntityIds.Contains(w.EntityId)).ToList();

                foreach (var existingEntityId in existingEntityIds)
                {
                    var importRequest = entityTypeRequestsDict[existingEntityId];
                    if (!request.OverwriteIfExists.GetValueOrDefault(false) || !importRequest.OverwriteIfExists.GetValueOrDefault(false)) continue;
                    existingEntityImportRequests.Add(importRequest);
                }

                if (newEntityImportRequests.Any())
                {
                    newEntityImportRequests.ForEach(importRequest =>
                    {
                        if (!importRequest.Data.ContainsKey(nameof(DataHubEntity.createdOn)))
                            importRequest.Data.Add(nameof(DataHubEntity.createdOn), now);

                        if (!importRequest.Data.ContainsKey(nameof(DataHubEntity.lastUpdated)))
                            importRequest.Data.Add(nameof(DataHubEntity.lastUpdated), now);
                    });

                    var untrackedEntities = newEntityImportRequests.Where(w => w.Untracked.GetValueOrDefault(false)).ToList();
                    var trackedEntities = newEntityImportRequests.Except(untrackedEntities).ToList();

                    if (trackedEntities.Any())
                    {
                        var trackingEntriesToAdd = trackedEntities.Select(s => new InitTrackedEntityRequest()
                        {
                            DataSource = DataSources.DataHub,
                            EntityType = s.EntityType,
                            EntityId = s.EntityId,
                            EntityData = s.Data.RemoveNullValues(),
                            Timestamp = s.Data.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated))
                        }).ToList();

                        _ = (await mediator.SendAsync(new InitTrackedEntitiesRequest()
                        {
                            Requests = trackingEntriesToAdd
                        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
                    }

                    var entitiesToCreate = newEntityImportRequests.Select(s => s.Data.RemoveNullValues()).ToList();
                    var createEntitiesResponse = (await mediator.TrySend(new CreateDataHubEntitiesCommand()
                    {
                        Entities = entitiesToCreate
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };


                    var response = entitiesToCreate.Select(dataHubEntity =>
                    {
                        var entityId = dataHubEntity.Value<string>(nameof(DataHubEntity.id));
                        var failure = createEntitiesResponse.Failures.FirstOrDefault(f => f.Item.Value<string>(nameof(DataHubEntity.id)) == entityId);

                        return new ImportEntityResponse()
                        {
                            EntityType = entityType,
                            EntityId = entityId,
                            Success = failure == null,
                            FailureReason = failure?.Error.Message
                        };
                    }).ToList();

                    responses.AddRange(response);
                }

                if (existingEntityImportRequests.Any())
                {
                    existingEntityImportRequests.ForEach(importRequest =>
                    {
                        if (!importRequest.Data.ContainsKey(nameof(DataHubEntity.createdOn)))
                            importRequest.Data.Add(nameof(DataHubEntity.createdOn), now);

                        if (!importRequest.Data.ContainsKey(nameof(DataHubEntity.lastUpdated)))
                            importRequest.Data.Add(nameof(DataHubEntity.lastUpdated), now);

                        if (request.Silent)
                        {
                            importRequest.Data[nameof(DataHubEntity.lastUpdated)] = existingEntityIdTimestamps[importRequest.EntityId];
                        }
                    });


                    var untrackedEntities = existingEntityImportRequests.Where(w => w.Untracked.GetValueOrDefault(false)).ToList();
                    var trackedEntities = existingEntityImportRequests.Except(untrackedEntities).ToList();

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

                            return new ImportEntityResponse()
                            {
                                EntityType = entityType,
                                EntityId = entityId,
                                Success = failure == null,
                                FailureReason = failure?.Error.Message
                            };
                        }).ToList();

                        responses.AddRange(response);
                    }

                    if (trackedEntities.Any())
                    {
                        


                    }
                }
            }

            return responses;
        }
        finally
        {
            if (dataHubEntityLocks != null) await processingLockService.ReleaseLocksAsync(dataHubEntityLocks, cancellationToken);
        }
    }
}
