using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesById;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRevertDataHubEntities;

public class ProcessRevertDataHubEntitiesRequestHandler(
    IMediator mediator,
    IProcessingLockService processingLockService,
    ITimeService timeService)
    : IHandler<ProcessRevertDataHubEntitiesRequest, ProcessRevertDataHubEntitiesResponse>
{
    public async Task<ProcessRevertDataHubEntitiesResponse> HandleAsync(ProcessRevertDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.TrackingEntryId))
        {
            var result = await ProcessTrackingEntryTarget(request, cancellationToken);
            return new ProcessRevertDataHubEntitiesResponse { Results = [result] };
        }

        var results = new List<RevertDataHubEntityResult>();
        var entityIdsToProcess = request.EntityIds.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct().ToList();

        while (entityIdsToProcess.Any())
        {
            var batch = entityIdsToProcess.Take(500).ToList();
            var trackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest
            {
                DataSource = DataSources.DataHub,
                EntityType = request.EntityType,
                EntityIds = batch
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var entriesByEntityId = trackingEntriesResponse.TrackingEntries
                .GroupBy(entry => entry.EntityId)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var entityId in batch)
            {
                entriesByEntityId.TryGetValue(entityId, out var entries);
                results.Add(await ProcessEntity(request, request.EntityType, entityId, entries ?? [], request.RevertTo, null, cancellationToken));
            }

            entityIdsToProcess.RemoveRange(0, batch.Count);
        }

        return new ProcessRevertDataHubEntitiesResponse
        {
            Results = results
        };
    }

    private async Task<RevertDataHubEntityResult> ProcessTrackingEntryTarget(ProcessRevertDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {
        var trackingEntryResponse = (await mediator.TrySend(new GetTrackingEntriesByIdQuery
        {
            Ids = [request.TrackingEntryId],
            PageSize = 1
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var trackingEntry = trackingEntryResponse.Results.FirstOrDefault();
        if (trackingEntry == null)
        {
            return new RevertDataHubEntityResult
            {
                TrackingEntryId = request.TrackingEntryId,
                Success = false,
                FailureReason = "TRACKING_ENTRY_NOT_FOUND"
            };
        }

        if (trackingEntry.DataSource != DataSources.DataHub)
        {
            return new RevertDataHubEntityResult
            {
                EntityType = trackingEntry.EntityType,
                EntityId = trackingEntry.EntityId,
                RevertedTo = trackingEntry.Timestamp,
                TrackingEntryId = trackingEntry.id,
                Success = false,
                FailureReason = "UNSUPPORTED_DATA_SOURCE"
            };
        }

        var trackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest
        {
            DataSource = DataSources.DataHub,
            EntityType = trackingEntry.EntityType,
            EntityIds = [trackingEntry.EntityId]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return await ProcessEntity(
            request,
            trackingEntry.EntityType,
            trackingEntry.EntityId,
            trackingEntriesResponse.TrackingEntries,
            trackingEntry.Timestamp,
            trackingEntry.id,
            cancellationToken);
    }

    private async Task<RevertDataHubEntityResult> ProcessEntity(
        ProcessRevertDataHubEntitiesRequest request,
        string entityType,
        string entityId,
        List<ChangeTrackingEntry> trackingEntries,
        DateTimeOffset? revertTo,
        string trackingEntryId,
        CancellationToken cancellationToken)
    {
        var result = new RevertDataHubEntityResult
        {
            EntityType = entityType,
            EntityId = entityId,
            RevertedTo = revertTo,
            TrackingEntryId = trackingEntryId
        };

        var entityLockId = $"entities/{DataSources.DataHub}/{entityType}/{entityId}";
        ProcessingLock entityLock = null;

        try
        {
            if (!request.DryRun)
            {
                var getLockResponse = await processingLockService.WaitForLockAsync(
                    entityLockId,
                    request.CorrelationId,
                    duration: TimeSpan.FromMinutes(5),
                    waitTimeOut: TimeSpan.FromMinutes(5),
                    cancellationToken: cancellationToken);
                getLockResponse.ThrowIfUnsuccessful();
                entityLock = getLockResponse.Result;

                var refreshedTrackingEntriesResponse = (await mediator.TrySend(new GetAllTrackingEntriesForEntitiesRequest
                {
                    DataSource = DataSources.DataHub,
                    EntityType = entityType,
                    EntityIds = [entityId]
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                trackingEntries = refreshedTrackingEntriesResponse.TrackingEntries;
            }

            var orderedEntries = OrderForReplay(trackingEntries);
            var targetEntries = GetTargetEntries(orderedEntries, revertTo, trackingEntryId);
            if (!targetEntries.Any(entry => entry.EntryType == ChangeTrackingEntryTypes.Init))
            {
                result.Success = false;
                result.FailureReason = "NO_TRACKING_STATE_AT_TARGET";
                return result;
            }

            JObject currentState;
            JObject targetState;
            try
            {
                currentState = ChangeTrackingHelper.ReassembleEntity(orderedEntries);
                targetState = ChangeTrackingHelper.ReassembleEntity(targetEntries);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.FailureReason = $"REPLAY_FAILED: {ex.Message}";
                return result;
            }

            var changeSet = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(currentState, targetState));
            result.ChangeSet = changeSet?.HasValues == true ? changeSet : null;
            result.Changed = result.ChangeSet != null;

            if (!result.Changed || request.DryRun)
            {
                result.Success = true;
                return result;
            }

            var revertTimestamp = timeService.Now();
            targetState[nameof(DataHubEntity.lastUpdated)] = revertTimestamp;

            _ = (await mediator.SendAsync(new AddTrackedEntityChangeSetRequest
            {
                DataSource = DataSources.DataHub,
                EntityType = entityType,
                SourceEntityId = entityId,
                ChangeSet = result.ChangeSet,
                TimeStamp = revertTimestamp
            }, cancellationToken)) switch { { IsT1: true } sendResult => throw sendResult.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

            var upsertResponse = (await mediator.SendAsync(new UpsertDataHubEntitiesCommand
            {
                Entities = [targetState.RemoveNullValues()]
            }, cancellationToken)) switch { { IsT1: true } sendResult => throw sendResult.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

            if (upsertResponse.Failures.Any())
            {
                result.Success = false;
                result.FailureReason = BuildPersistenceFailureReason("Failed to upsert DataHub entity", upsertResponse.Failures.Select(failure => failure.Error?.Message));
                return result;
            }

            if (request.DispatchNotifications)
            {
                _ = (await mediator.SendAsync(new DispatchNotificationsRequest
                {
                    Notifications =
                    [
                        new DataHubEntityUpdatedNotification
                        {
                            DataSource = DataSources.DataHub,
                            DataHubEntityType = entityType,
                            DataHubEntityId = entityId,
                            UpdatePaths = result.ChangeSet.Properties().Select(property => property.Name).ToList()
                        }
                    ]
                }, cancellationToken)) switch { { IsT1: true } sendResult => throw sendResult.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
            }

            result.RevertedTo = revertTo;
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.FailureReason = ex.Message;
            return result;
        }
        finally
        {
            if (entityLock != null)
            {
                await processingLockService.ReleaseLockAsync(entityLock, cancellationToken);
            }
        }
    }

    private static List<ChangeTrackingEntry> GetTargetEntries(List<ChangeTrackingEntry> orderedEntries, DateTimeOffset? revertTo, string trackingEntryId)
    {
        if (!string.IsNullOrWhiteSpace(trackingEntryId))
        {
            var index = orderedEntries.FindIndex(entry => entry.id == trackingEntryId);
            return index < 0 ? [] : orderedEntries.Take(index + 1).ToList();
        }

        return revertTo.HasValue
            ? orderedEntries.Where(entry => entry.Timestamp <= revertTo.Value).ToList()
            : [];
    }

    private static List<ChangeTrackingEntry> OrderForReplay(IEnumerable<ChangeTrackingEntry> entries)
    {
        return entries
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => entry._ts == 0 ? long.MaxValue : entry._ts)
            .ThenBy(entry => entry.id)
            .ToList();
    }

    private static string BuildPersistenceFailureReason(string prefix, IEnumerable<string> errors)
    {
        var errorText = string.Join("; ", errors.Where(w => !string.IsNullOrWhiteSpace(w)));
        return string.IsNullOrWhiteSpace(errorText) ? prefix : $"{prefix}: {errorText}";
    }
}
