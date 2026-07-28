using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessSplitDataHubEntityAlternateKey;

public class ProcessSplitDataHubEntityAlternateKeyRequestHandler(
    IMediator mediator,
    IProcessingLockService processingLockService,
    IIdService idService,
    ITimeService timeService)
    : IHandler<ProcessSplitDataHubEntityAlternateKeyRequest, ProcessSplitDataHubEntityAlternateKeyResponse>
{
    public async Task<ProcessSplitDataHubEntityAlternateKeyResponse> HandleAsync(ProcessSplitDataHubEntityAlternateKeyRequest request, CancellationToken cancellationToken)
    {
        var newEntityId = idService.NewId<DataHubEntity>();
        var result = CreateResult(request, newEntityId);
        var locks = new List<ProcessingLock>();

        try
        {
            if (!request.DryRun)
            {
                locks.Add(await AcquireEntityLock(request.CorrelationId, DataSources.DataHub, request.EntityType, request.EntityId, cancellationToken));
                locks.Add(await AcquireEntityLock(request.CorrelationId, DataSources.DataHub, request.EntityType, newEntityId, cancellationToken));
            }

            var getEntitiesResponse = (await mediator.TrySend(new GetMaterializedEntitiesByIdRequest
            {
                EntityType = request.EntityType,
                EntityIds = [request.EntityId]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var originalEntity = getEntitiesResponse.Results.FirstOrDefault();
            if (originalEntity == null)
            {
                return Fail(result, "ENTITY_NOT_FOUND");
            }

            var alternateKeys = originalEntity.Value<JArray>(nameof(DataHubEntity.alternateKeys)) ?? new JArray();
            var matchingKeys = alternateKeys
                .Children<JObject>()
                .Where(altKey => IsAlternateKey(altKey, request.Key, request.Value))
                .ToList();

            if (matchingKeys.Count == 0)
            {
                return Fail(result, "ALTERNATE_KEY_NOT_FOUND");
            }

            if (matchingKeys.Count > 1)
            {
                return Fail(result, "ALTERNATE_KEY_NOT_UNIQUE");
            }

            var hasDuplicateValue = alternateKeys
                .Children<JObject>()
                .Any(altKey => !ReferenceEquals(altKey, matchingKeys[0]) &&
                               string.Equals(altKey.Value<string>(nameof(AlternateKey.Value)), request.Value, StringComparison.Ordinal));

            if (!hasDuplicateValue)
            {
                return Fail(result, "ALTERNATE_KEY_VALUE_NOT_DUPLICATED");
            }

            var selectedAlternateKey = (JObject)matchingKeys[0].DeepClone();

            var trackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest
            {
                DataSource = DataSources.DataHub,
                EntityType = request.EntityType,
                EntityIds = [request.EntityId]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var trackingEntries = trackingEntriesResponse.TrackingEntries
                .Where(entry => entry.DataSource == DataSources.DataHub &&
                                entry.EntityType == request.EntityType &&
                                entry.EntityId == request.EntityId)
                .ToList();

            var originalUpdate = BuildOriginalUpdate(originalEntity, selectedAlternateKey, request.Silent, timeService.Now());
            var splitEntity = BuildSplitEntity(originalEntity, newEntityId, selectedAlternateKey);
            var clonedTrackingEntries = CloneTrackingEntries(trackingEntries, newEntityId, selectedAlternateKey);

            result.Success = true;
            result.Changed = true;
            result.CopiedTrackingEntries = clonedTrackingEntries.Count;

            if (request.DryRun)
            {
                return result;
            }

            var documentsToUpsert = new List<JObject> { originalUpdate.Entity, splitEntity };
            var upsertEntitiesResponse = (await mediator.TrySend(new UpsertDataHubEntitiesCommand
            {
                Entities = documentsToUpsert
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (upsertEntitiesResponse.Failures.Any())
            {
                return Fail(result, BuildPersistenceFailureReason("Failed to upsert DataHub entities", upsertEntitiesResponse.Failures.Select(failure => failure.Error?.Message)));
            }

            var trackingDocumentsToUpsert = new List<ChangeTrackingEntry>(clonedTrackingEntries);
            if (!request.Silent)
            {
                trackingDocumentsToUpsert.Add(originalUpdate.TrackingEntry);
            }

            if (trackingDocumentsToUpsert.Any())
            {
                var upsertTrackingResponse = (await mediator.TrySend(new UpsertCosmosDocumentsCommand<ChangeTrackingEntry>
                {
                    Documents = trackingDocumentsToUpsert
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                if (upsertTrackingResponse.Failures.Any())
                {
                    return Fail(result, BuildPersistenceFailureReason("Failed to upsert tracking entries", upsertTrackingResponse.Failures.Select(failure => failure.Error?.Message)));
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return Fail(result, ex.Message);
        }
        finally
        {
            foreach (var entityLock in locks.Where(entityLock => entityLock != null))
            {
                await processingLockService.ReleaseLockAsync(entityLock, cancellationToken);
            }
        }
    }

    private async Task<ProcessingLock> AcquireEntityLock(string correlationId, string dataSource, string entityType, string entityId, CancellationToken cancellationToken)
    {
        var lockId = $"entities/{dataSource}/{entityType}/{entityId}";
        var getLockResponse = await processingLockService.WaitForLockAsync(
            lockId,
            correlationId,
            duration: TimeSpan.FromMinutes(5),
            waitTimeOut: TimeSpan.FromMinutes(5),
            cancellationToken: cancellationToken);
        getLockResponse.ThrowIfUnsuccessful();
        return getLockResponse.Result;
    }

    private static ProcessSplitDataHubEntityAlternateKeyResponse CreateResult(ProcessSplitDataHubEntityAlternateKeyRequest request, string newEntityId)
    {
        return new ProcessSplitDataHubEntityAlternateKeyResponse
        {
            EntityType = request.EntityType,
            OriginalEntityId = request.EntityId,
            NewEntityId = newEntityId,
            Key = request.Key,
            Value = request.Value
        };
    }

    private static ProcessSplitDataHubEntityAlternateKeyResponse Fail(ProcessSplitDataHubEntityAlternateKeyResponse result, string failureReason)
    {
        result.Success = false;
        result.Changed = false;
        result.FailureReason = failureReason;
        return result;
    }

    private static bool IsAlternateKey(JObject alternateKey, string key, string value)
    {
        return string.Equals(alternateKey.Value<string>(nameof(AlternateKey.Key)), key, StringComparison.Ordinal) &&
               string.Equals(alternateKey.Value<string>(nameof(AlternateKey.Value)), value, StringComparison.Ordinal);
    }

    private static SplitOriginalUpdate BuildOriginalUpdate(JObject originalEntity, JObject selectedAlternateKey, bool silent, DateTimeOffset now)
    {
        var updatedEntity = (JObject)originalEntity.DeepClone();
        var updatedAlternateKeys = new JArray(
            (updatedEntity.Value<JArray>(nameof(DataHubEntity.alternateKeys)) ?? new JArray())
            .Children<JObject>()
            .Where(altKey => !JToken.DeepEquals(altKey, selectedAlternateKey))
            .Select(altKey => altKey.DeepClone()));

        var originalTimestamp = originalEntity.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated));
        var updateTimestamp = originalTimestamp.HasValue && originalTimestamp.Value > now ? originalTimestamp.Value : now;
        updatedEntity[nameof(DataHubEntity.alternateKeys)] = updatedAlternateKeys;
        if (!silent)
        {
            updatedEntity[nameof(DataHubEntity.lastUpdated)] = updateTimestamp;
        }

        var originalAlternateKeys = originalEntity.Value<JArray>(nameof(DataHubEntity.alternateKeys)) ?? new JArray();
        var alternateKeyDiff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(originalAlternateKeys, updatedAlternateKeys);
        var trackingEntry = new ChangeTrackingEntry
        {
            EntryType = ChangeTrackingEntryTypes.Update,
            EntityType = originalEntity.DataHubEntityType(),
            EntityId = originalEntity.DataHubEntityId(),
            DataSource = DataSources.DataHub,
            Timestamp = updateTimestamp,
            Data = new JObject
            {
                [nameof(DataHubEntity.alternateKeys)] = alternateKeyDiff
            }
        };

        return new SplitOriginalUpdate(updatedEntity, trackingEntry);
    }

    private static JObject BuildSplitEntity(JObject originalEntity, string newEntityId, JObject selectedAlternateKey)
    {
        var splitEntity = (JObject)originalEntity.DeepClone();
        splitEntity[nameof(DataHubEntity.id)] = newEntityId;
        splitEntity[nameof(DataHubEntity.alternateKeys)] = new JArray(selectedAlternateKey.DeepClone());
        splitEntity.Remove(nameof(CosmosDocument._etag));
        splitEntity.Remove("_ts");
        splitEntity.Remove("_dnf");
        return splitEntity;
    }

    private string NewTrackingEntryId()
    {
        return idService.NewId<ChangeTrackingEntry>();
    }

    private List<ChangeTrackingEntry> CloneTrackingEntries(IEnumerable<ChangeTrackingEntry> trackingEntries, string newEntityId, JObject selectedAlternateKey)
    {
        return trackingEntries
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => entry._ts == 0 ? long.MaxValue : entry._ts)
            .ThenBy(entry => entry.id)
            .Select(entry => CloneTrackingEntry(entry, newEntityId, selectedAlternateKey))
            .ToList();
    }

    private ChangeTrackingEntry CloneTrackingEntry(ChangeTrackingEntry entry, string newEntityId, JObject selectedAlternateKey)
    {
        var data = entry.Data == null ? new JObject() : (JObject)entry.Data.DeepClone();
        if (entry.EntryType == ChangeTrackingEntryTypes.Init)
        {
            data[nameof(DataHubEntity.alternateKeys)] = new JArray(selectedAlternateKey.DeepClone());
        }
        else
        {
            data.Remove(nameof(DataHubEntity.alternateKeys));
        }

        return new ChangeTrackingEntry
        {
            id = NewTrackingEntryId(),
            EntryType = entry.EntryType,
            EntityType = entry.EntityType,
            EntityId = newEntityId,
            DataSource = entry.DataSource,
            Timestamp = entry.Timestamp,
            Data = data
        };
    }

    private static string BuildPersistenceFailureReason(string prefix, IEnumerable<string> errors)
    {
        var errorText = string.Join("; ", errors.Where(w => !string.IsNullOrWhiteSpace(w)));
        return string.IsNullOrWhiteSpace(errorText) ? prefix : $"{prefix}: {errorText}";
    }

    private sealed record SplitOriginalUpdate(JObject Entity, ChangeTrackingEntry TrackingEntry);
}
