using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;

public class ProcessPatchEntityRequestHandler(IMediator mediator, IProcessingLockService processingLockService, ITimeService timeService)
    : IHandler<ProcessPatchEntityRequest, ProcessPatchEntityResponse>
{
    public async Task<ProcessPatchEntityResponse> HandleAsync(ProcessPatchEntityRequest request, CancellationToken cancellationToken)
    {
        var entityLockId = $"entities/{request.DataSource}/{request.EntityType}/{request.EntityId}";
        ProcessingLock entityLock = null;

        try
        {
            var getLockResponse = await processingLockService.WaitForLockAsync(entityLockId, request.CorrelationId, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            getLockResponse.ThrowIfUnsuccessful();
            entityLock = getLockResponse.Result;

            var isDataHubEntity = request.DataSource == DataSources.DataHub;
            var isTrackedEntity = true;

            JObject originalEntity = null;

            List<ChangeTrackingEntry> trackingEntryCache = null;
            if (request.Cache.TryGetValue("TrackingEntries", out var cacheEntry))
            {
                trackingEntryCache = (List<ChangeTrackingEntry>)cacheEntry;
            }
            ;

            switch (request.DataSource)
            {
                case DataSources.DataHub:
                    if (request.Cache.TryGetValue("DataHubEntities", out var materializedEntities))
                    {
                        originalEntity = ((List<JObject>)materializedEntities).FirstOrDefault(f => f.DataHubEntityType() == request.EntityType && f.DataHubEntityId() == request.EntityId);
                    }

                    if (originalEntity == null)
                    {
                        var getMaterializedEntitiesResponse = (await mediator.TrySend(new GetMaterializedEntitiesByIdRequest()
                        {
                            EntityType = request.EntityType,
                            EntityIds = [request.EntityId]
                        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                        originalEntity = getMaterializedEntitiesResponse.Results.FirstOrDefault();
                    }

                    isTrackedEntity = trackingEntryCache?.Any(w => w.DataSource == request.DataSource && w.EntityType == request.EntityType && w.EntityId == request.EntityId) ?? false;
                    break;


                default:
                    originalEntity = (await mediator.TrySend(new GetTrackedEntityRequest()
                    {
                        DataSource = request.DataSource,
                        EntityType = request.EntityType,
                        EntityId = request.EntityId,
                        TrackingEntries = trackingEntryCache?.Where(w => w.DataSource == request.DataSource && w.EntityType == request.EntityType && w.EntityId == request.EntityId).ToList()
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                    break;
            }

            if (originalEntity == null)
            {
                return new ProcessPatchEntityResponse()
                {
                    Success = false,
                    FailureReason = "ENTITY_NOT_FOUND"
                };
            }

            var originalTimestamp = originalEntity!.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated));
            var updatingEntity = (JObject)originalEntity.DeepClone();

            if (updatingEntity == null) throw new Exception("Updated entity not found");

            var failures = new List<PatchFailure>();

            foreach (var operation in request.Operations)
            {
                try
                {
                    var token = updatingEntity.SelectToken(operation.Path);

                    switch (operation.Operation?.ToLower())
                    {
                        case "add":
                            if (token != null)
                                throw new Exception("Path already exists");

                            ProcessAdd(operation, updatingEntity, originalEntity);
                            continue;


                        case "set":
                            if (token == null)
                            {
                                ProcessAdd(operation, updatingEntity, originalEntity);
                            }
                            else
                            {
                                ProcessSet(operation, token, originalEntity);
                            }

                            continue;


                        case "remove":
                            if (token != null)
                            {
                                if (token.Parent?.Type == JTokenType.Array)
                                {
                                    ((JArray)token.Parent).Remove(token);
                                    continue;
                                }

                                token.Parent!.Remove();
                            }

                            continue;


                        default:
                            throw new ArgumentException($"{operation.Operation} not supported");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(new PatchFailure()
                    {
                        Patch = operation,
                        FailureReason = ex.Message
                    });
                }
            }

            JObject changeSet = null;

            var dhEntityDiffs = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(originalEntity, updatingEntity));
            if (dhEntityDiffs?.HasValues == true)
            {
                changeSet = dhEntityDiffs;
            }

            var updateTimestamp = request.Silent ? originalTimestamp : request.Timestamp ?? timeService.Now();
            updatingEntity[nameof(DataHubEntity.lastUpdated)] = updateTimestamp;

            if (changeSet == null || !request.CommitToDb)
            {
                return new ProcessPatchEntityResponse()
                {
                    RequestId = request.RequestId,
                    Success = !failures.Any(),
                    PatchFailures = failures,
                    IsTrackedEntity = isTrackedEntity,
                    IsDataHubEntity = request.DataSource == DataSources.DataHub,
                    ChangeSet = changeSet,
                    UpdatedEntity = updatingEntity,
                    Notify = request.DispatchNotifications && isDataHubEntity && changeSet != null && updatingEntity != null
                };
            }

            if (isTrackedEntity && !request.DoNotTrack)
            {
                _ = (await mediator.SendAsync(new AddTrackedEntityChangeSetRequest()
                {
                    DataSource = request.DataSource,
                    EntityType = request.EntityType,
                    SourceEntityId = request.EntityId,
                    ChangeSet = changeSet,
                    TimeStamp = request.Timestamp ?? timeService.Now()
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
            }

            if (request.DataSource == DataSources.DataHub)
            {
                var upsertResponse = (await mediator.SendAsync(new UpsertDataHubEntitiesCommand()
                {
                    Entities = [updatingEntity]
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

                if (upsertResponse.Failures.Any())
                {
                    return new ProcessPatchEntityResponse()
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        FailureReason = BuildPersistenceFailureReason("Failed to upsert DataHub entity", upsertResponse.Failures.Select(s => s.Error?.Message)),
                        PatchFailures = failures,
                        IsTrackedEntity = isTrackedEntity,
                        IsDataHubEntity = isDataHubEntity,
                        ChangeSet = changeSet,
                        UpdatedEntity = updatingEntity,
                        Notify = false
                    };
                }
            }

            if (request.DispatchNotifications && isDataHubEntity)
            {
                _ = (await mediator.SendAsync(new DispatchNotificationsRequest()
                {
                    Notifications =
                    [
                        new DataHubEntityUpdatedNotification()
                        {
                            DataSource = request.DataSource,
                            DataHubEntityType = request.EntityType,
                            DataHubEntityId = request.EntityId,
                            UpdatePaths = dhEntityDiffs?.Properties().Select(s => s.Name).ToList()
                        }
                    ]
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
            }

            if (failures.Any())
            {
                return new ProcessPatchEntityResponse()
                {
                    RequestId = request.RequestId,
                    Success = false,
                    FailureReason = "ONE_OR_MORE_PATCH_OPERATIONS_FAILED",
                    PatchFailures = failures,
                    IsTrackedEntity = isTrackedEntity,
                    IsDataHubEntity = isDataHubEntity,
                    ChangeSet = changeSet,
                    UpdatedEntity = updatingEntity,
                    Notify = request.DispatchNotifications && isDataHubEntity && changeSet != null && updatingEntity != null
                };

            }

            return new ProcessPatchEntityResponse()
            {
                RequestId = request.RequestId,
                Success = true,
                IsTrackedEntity = isTrackedEntity,
                IsDataHubEntity = isDataHubEntity,
                ChangeSet = changeSet,
                UpdatedEntity = updatingEntity,
                Notify = request.DispatchNotifications && isDataHubEntity && changeSet != null && updatingEntity != null
            };
        }
        catch (Exception ex)
        {
            return new ProcessPatchEntityResponse()
            {
                RequestId = request.RequestId,
                IsDataHubEntity = request.DataSource == DataSources.DataHub,
                FailureReason = ex.Message,
                Success = false
            };
        }
        finally
        {
            if (entityLock != null) await processingLockService.ReleaseLockAsync(entityLock, cancellationToken);
        }
    }

    private void ProcessAdd(Patch operation, JObject updatedEntity, JObject originalEntity)
    {
        var pathParts = operation.Path.Split('.');
        var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
        var childPath = pathParts.Last();

        if (childPath.EndsWith("]"))
        {
            parentPath = operation.Path[..operation.Path.LastIndexOf("[", StringComparison.Ordinal)];
        }

        var parent = string.IsNullOrEmpty(parentPath) ? updatedEntity : updatedEntity.SelectToken(parentPath);
        if (parent == null)
        {
            var tmpParent = updatedEntity.Root;
            foreach (var pathPart in pathParts.Take(pathParts.Length - 1))
            {
                if (tmpParent[pathPart] == null)
                {
                    // Determine if the next part is an array index or an object key
                    if (pathParts.SkipWhile(p => p != pathPart).Skip(1).FirstOrDefault()?.EndsWith("]") == true)
                    {
                        tmpParent[pathPart] = new JArray();
                    }
                    else
                    {
                        tmpParent[pathPart] = new JObject();
                    }
                }
                tmpParent = tmpParent[pathPart];
            }

            parent = tmpParent;
        }

        if (parent == null)
        {
            throw new Exception("Invalid path");
        }

        var value = operation.Value ?? JValue.CreateNull();

        if (parent.Type == JTokenType.Array)
        {
            var openBracketIndex = childPath.IndexOf("[", StringComparison.Ordinal);
            var index = int.Parse(childPath.Substring(openBracketIndex + 1, childPath.Length - openBracketIndex - 2));
            ((JArray)parent).Insert(index, value);
        }
        else
        {
            if (value.Type == JTokenType.String && value.Value<string>().StartsWith("^.", StringComparison.Ordinal))
            {
                var valuePath = value.Value<string>().Substring(2);
                var val = originalEntity.SelectToken(valuePath);
                parent[childPath] = val ?? JValue.CreateNull();
                return;
            }

            parent[childPath] = value;
        }
    }

    private void ProcessSet(Patch operation, JToken token, JObject originalEntity)
    {
        var value = operation.Value ?? JValue.CreateNull();

        if (token.Type == JTokenType.String)
        {
            if (!string.IsNullOrEmpty(operation.Regex))
            {
                var input = token.Value<string>();
                var replacement = value.Value<string>();
                var result = Regex.Replace(input, operation.Regex, replacement);
                token.Replace(result);
                return;
            }

            if (value.Type == JTokenType.String && value.Value<string>().StartsWith("^.", StringComparison.Ordinal))
            {
                var valuePath = value.Value<string>().Substring(2);
                value = originalEntity.SelectToken(valuePath) ?? JValue.CreateNull();
            }
        }

        if (token is JValue { Type: JTokenType.Date } dateToken && value is JValue { Type: JTokenType.Date } replacementDate)
        {
            // JToken.Replace can treat DateTime and DateTimeOffset values with equal
            // clock ticks as unchanged. Assign the value to preserve the actual instant,
            // offset and DateTime kind even when that equality shortcut would apply.
            dateToken.Value = replacementDate.Value;
        }
        else
        {
            token.Replace(value);
        }
    }

    private static string BuildPersistenceFailureReason(string prefix, IEnumerable<string> errors)
    {
        var errorText = string.Join("; ", errors.Where(w => !string.IsNullOrWhiteSpace(w)));
        return string.IsNullOrWhiteSpace(errorText) ? prefix : $"{prefix}: {errorText}";
    }

}
