using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataHub.SharedModels.Rules;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdatedEntities;

public class ProcessUpdatedEntitiesRequestHandler(IIdService idService, IMediator mediator, IProcessingLockService processingLockService, ITimeService timeService)
    : IHandler<ProcessUpdatedEntitiesRequest, ProcessUpdatedEntitiesResponse>
{
    public async Task<ProcessUpdatedEntitiesResponse> HandleAsync(ProcessUpdatedEntitiesRequest request, CancellationToken cancellationToken)
    {
        var (results, dataHubEntityIds, resolvedEntityIdMap) = InitializeVars(request);

        var dataHubEntityLockIds = dataHubEntityIds.Select(entityId => $"entities/{DataSources.DataHub}/{request.DataHubEntityType}/{entityId}").ToList();
        List<ProcessingLock> dataHubEntityLocks = null;
        try
        {
            var getLocksResponse = await processingLockService.WaitForLocksAsync(dataHubEntityLockIds, request.CorrelationId, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            getLocksResponse.ThrowIfUnsuccessful();
            dataHubEntityLocks = getLocksResponse.Result;

            var currentDataHubEntities = await GetDataHubEntities(request.DataHubEntityType, dataHubEntityIds, cancellationToken);
            var currentDataHubEntityChangeTrackingEntries = await DataHubEntityTrackingEntries(request, dataHubEntityIds);

            var acceptedChangeTrackingEntries = new List<ChangeTrackingEntry>();
            var updatedDataHubEntities = await CalculateUpdatedDataHubEntities(request, currentDataHubEntities, currentDataHubEntityChangeTrackingEntries, acceptedChangeTrackingEntries, results, resolvedEntityIdMap, cancellationToken);

            if (!request.DoNotTrack)
            {
                // Consolidate the projection writes, never the individual accepted history entries.
                await CommitDataHubEntityChangeTrackingToDb(acceptedChangeTrackingEntries, cancellationToken);
            }

            await CommitUpdatedDataHubEntitiesToDb(updatedDataHubEntities.Values.ToList(), currentDataHubEntities, request, resolvedEntityIdMap, cancellationToken, results);

            return new ProcessUpdatedEntitiesResponse()
            {
                Results = results
            };
        }
        finally
        {
            if (dataHubEntityLocks != null) await processingLockService.ReleaseLocksAsync(dataHubEntityLocks, cancellationToken);
        }
    }

    private async Task<List<JObject>> GetDataHubEntities(string dataHubEntityType, List<string> dataHubEntityIds, CancellationToken cancellationToken)
    {
        var getDataHubEntitiesResponse = (await mediator.TrySend(new GetDataHubEntitiesByIdRequest()
        {
            EntityType = dataHubEntityType,
            EntityIds = dataHubEntityIds
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return getDataHubEntitiesResponse.Results;
    }

    #region Private Helpers

    private void CalculateAndAddDataHubEntityChangeTracking(List<ChangeTrackingEntry> acceptedChangeTrackingEntries, JToken fromEntity, JObject toEntity, EntityReference dataHubEntityRef, DateTimeOffset timestamp)
    {
        var changeSet = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(fromEntity, toEntity);

        if (changeSet != null)
        {
            acceptedChangeTrackingEntries.Add(new ChangeTrackingEntry()
            {
                id = idService.NewId<ChangeTrackingEntry>(),
                EntryType = ChangeTrackingEntryTypes.Update,
                EntityType = dataHubEntityRef.EntityType,
                EntityId = dataHubEntityRef.EntityId,
                DataSource = DataSources.DataHub,
                Timestamp = timestamp,
                Data = (JObject)changeSet
            });
        }
    }

    private void AddSourceEntityToDataHubEntityAlternateKeys(ProcessUpdatedEntitiesRequest request, AddTrackedEntityChangeSetRequest sourceEntityChange, JToken updatedDataHubEntity)
    {
        var sourceEntityAlternateKeys = sourceEntityChange.SourceEntityAlternateKeys
            ?? request.MergeRequests.First(f => f.SourceEntityId == sourceEntityChange.SourceEntityId)
                .Data.Value<JArray>(nameof(DataHubEntity.alternateKeys));
        var dataHubEntityAlternateKeys = updatedDataHubEntity.Value<JArray>(nameof(DataHubEntity.alternateKeys));

        if (sourceEntityAlternateKeys != null)
        {
            foreach (var sourceEntityAltKey in sourceEntityAlternateKeys.ToList())
            {
                var existingAltKey = dataHubEntityAlternateKeys.FirstOrDefault(dataHubEntityAltKey => dataHubEntityAltKey.Value<string>(nameof(AlternateKey.Key)) == sourceEntityAltKey.Value<string>(nameof(AlternateKey.Key)));
                if (existingAltKey == null)
                {
                    dataHubEntityAlternateKeys.Add(sourceEntityAltKey);
                    continue;
                }

                existingAltKey[nameof(AlternateKey.Value)] = sourceEntityAltKey[nameof(AlternateKey.Value)];
            }
        }
    }

    private void AddMergeFailedResult(ProcessUpdatedEntitiesRequest request, List<MergeEntityResult> results, string dataHubEntityId, Dictionary<string, ExternalEntityReference> resolvedEntityIdMap, Exception ex)
    {
        results.Add(new MergeEntityResult()
        {
            DataSource = request.DataSource,
            SourceEntityType = request.SourceEntityType,
            DataHubEntityType = request.DataHubEntityType,
            MergeOutcome = MergeOutcomes.MergeFailed,
            DataHubEntityId = dataHubEntityId,
            SourceEntityId = resolvedEntityIdMap[dataHubEntityId].EntityId,
            FailureReason = ex?.Message
        });
    }

    private async Task CommitUpdatedDataHubEntitiesToDb(List<JObject> updatedDataHubEntities, List<JObject> originalDataHubEntities, ProcessUpdatedEntitiesRequest request, Dictionary<string, ExternalEntityReference> resolvedEntityIdMap, CancellationToken cancellationToken, List<MergeEntityResult> results)
    {
        if (updatedDataHubEntities.Any())
        {
            updatedDataHubEntities.ForEach(e => { e[nameof(DataHubEntity.lastUpdated)] = timeService.Now(); });

            var upsertResponse = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
            {
                Entities = updatedDataHubEntities.Select(s => s.RemoveNullValues()).ToList()
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            results.AddRange(updatedDataHubEntities.Select(s =>
            {
                var dataHubEntityId = s.DataHubEntityId();
                var sourceEntityId = resolvedEntityIdMap[dataHubEntityId].EntityId;
                var failure = upsertResponse.Failures.FirstOrDefault(f => f.Item.DataHubEntityId() == dataHubEntityId);
                if (failure != null)
                {
                    // A failed final projection invalidates all source changes feeding it.
                    foreach (var pair in request.SourceChangesByTrackingEntry.Where(p => p.Key.EntityId == dataHubEntityId))
                        pair.Value.ExcludeFromSourceHistory = true;
                    foreach (var sourceChange in request.SourceEntityChangesWithAlternateKeys.Where(c =>
                                 request.ResolvedReferencedEntities.Any(r => r.SourceEntityReference.EntityId == c.SourceEntityId && r.DataHubEntityReference.EntityId == dataHubEntityId)))
                        sourceChange.ExcludeFromSourceHistory = true;
                }
                var original = originalDataHubEntities.First(e => e.DataHubEntityId() == dataHubEntityId);
                var changeSet = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(original, s) as JObject;
                return new MergeEntityResult()
                {
                    DataSource = request.DataSource,
                    SourceEntityType = request.SourceEntityType,
                    SourceEntityId = sourceEntityId,
                    DataHubEntityType = request.DataHubEntityType,
                    MergeOutcome = failure == null ? MergeOutcomes.EntityMatchedAndUpdated : MergeOutcomes.MergeFailed,
                    FailureReason = failure?.Error?.Message,
                    DataHubEntityId = dataHubEntityId,
                    ResultingDataHubEntity = s,
                    ResultingDataHubEntityUpdates = changeSet == null ? null : ChangeTrackingHelper.StripBaseProperties(changeSet)
                };
            }));
        }
    }

    private async Task CommitDataHubEntityChangeTrackingToDb(List<ChangeTrackingEntry> changeTrackingEntries, CancellationToken cancellationToken)
    {
        if (!changeTrackingEntries.Any()) return;

        var response = (await mediator.SendAsync(new AddTrackedEntityChangeSetsRequest()
        {
            Requests = changeTrackingEntries.Select(s => new AddTrackedEntityChangeSetRequest()
            {
                DataSource = s.DataSource,
                EntityType = s.EntityType,
                SourceEntityId = s.EntityId,
                TimeStamp = s.Timestamp,
                ChangeSet = ChangeTrackingHelper.StripBaseProperties(s.Data)
            }).ToList()
        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
        if (response.Failures.Any()) throw response.Failures.First().Error;
    }

    private async Task<Dictionary<string, JObject>> CalculateUpdatedDataHubEntities(ProcessUpdatedEntitiesRequest request, List<JObject> dataHubEntities, List<ChangeTrackingEntry> trackingEntityCache, List<ChangeTrackingEntry> acceptedChangeTrackingEntries, List<MergeEntityResult> results, Dictionary<string, ExternalEntityReference> resolvedEntityIdMap, CancellationToken cancellationToken)
    {
        var workingEntities = dataHubEntities.ToDictionary(e => e.DataHubEntityId());
        var updatedDataHubEntities = new Dictionary<string, JObject>();
        var skippedDestinationFields = new Dictionary<string, JObject>();
        var mergeRules = MergeRules(request.EntityConfig, request.DataSource, request.SourceEntityType);
        var doNotProcessAltKeys = mergeRules?.Rules.Any(r => r.PropertyName == nameof(DataHubEntity.alternateKeys) &&
            (r.Action == PropertyMergeRuleActions.DoNotUpdate || r.Action == PropertyMergeRuleActions.NeverOverwrite)) ?? false;
        foreach (var changeSet in request.ConvertedSourceEntityChanges.ToList())
        {
            try
            {
                var targetEntity = workingEntities[changeSet.EntityId];
                request.SourceChangesByTrackingEntry.TryGetValue(changeSet, out var sourceChange);
                JObject skippedFields = null;
                if (sourceChange != null) skippedDestinationFields.TryGetValue(sourceChange.SourceEntityId, out skippedFields);
                var hasAlternateKeyChanges = sourceChange != null &&
                    (request.SourceEntityChangesWithAlternateKeys.Contains(sourceChange) ||
                     (skippedFields?.ContainsKey(nameof(DataHubEntity.alternateKeys)) == true && sourceChange.SourceEntityAlternateKeys != null));
                if (sourceChange?.SourceEntityInput != null && skippedFields != null)
                {
                    // Reconsider supplied fields affected by rejection, plus the normal
                    // source delta. Unrelated unchanged snapshot fields stay untouched.
                    var suppliedFields = MergeBatchIntegrity.SelectSuppliedChanges(sourceChange.SourceEntityInput, changeSet.Data, skippedFields);
                    suppliedFields.Remove(nameof(DataHubEntity.alternateKeys));
                    var rebased = MergeBatchIntegrity.MergeInput(targetEntity, suppliedFields);
                    changeSet.Data = ChangeTrackingHelper.StripBaseProperties(
                        ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(targetEntity, rebased) as JObject) ?? new JObject();
                }
                // Retained no-op inputs only need processing if a preceding event
                // was rejected, or if they carry an actual alternate-key transition.
                if (!changeSet.Data.HasValues && !hasAlternateKeyChanges) continue;

                #region Process Merge Rules

                if (request.EntityConfig != null)
                {
                    var checkResponse = (await mediator.TrySend(new CheckPreMergeRulesRequest
                    {
                        DataSource = request.DataSource,
                        EntityConfig = request.EntityConfig,
                        DataHubEntityType = request.DataHubEntityType,
                        SourceEntityType = request.SourceEntityType,
                        IncomingEntity = changeSet.Data,
                        ExistingEntity = targetEntity,
                        ChangeSet = changeSet,
                        Context = MergeContexts.ExistingEntityPreMerge
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    if (!checkResponse.Pass)
                    {
                        switch (checkResponse.Action)
                        {
                            case PreMergeRuleActions.RejectMerge:
                                MarkSkippedDestination(request, changeSet, skippedDestinationFields);
                                ExcludeSourceChange(request, changeSet);
                                results.Add(new MergeEntityResult()
                                {
                                    DataSource = request.DataSource,
                                    SourceEntityType = request.SourceEntityType,
                                    DataHubEntityType = request.DataHubEntityType,
                                    MergeOutcome = MergeOutcomes.MergeRejected,
                                    DataHubEntityId = changeSet.EntityId,
                                    SourceEntityId = resolvedEntityIdMap[changeSet.EntityId].EntityId
                                });

                                continue;

                            case PreMergeRuleActions.SilentlyRejectMerge:
                                MarkSkippedDestination(request, changeSet, skippedDestinationFields);
                                results.Add(new MergeEntityResult()
                                {
                                    DataSource = request.DataSource,
                                    SourceEntityType = request.SourceEntityType,
                                    DataHubEntityType = request.DataHubEntityType,
                                    MergeOutcome = MergeOutcomes.MergeSilentlyRejected,
                                    DataHubEntityId = changeSet.EntityId,
                                    SourceEntityId = resolvedEntityIdMap[changeSet.EntityId].EntityId
                                });

                                continue;
                        }
                    }

                    if (mergeRules != null)
                    {
                        var updates = changeSet.Data;

                        var props = updates.GetAllProperties();

                        foreach (var prop in props)
                        {
                            var propertyMergeRule = PropertyMergeRuleResolver.Resolve(mergeRules, prop.Key);
                            if (propertyMergeRule != null)
                            {
                                switch (propertyMergeRule.Action)
                                {
                                    case PropertyMergeRuleActions.AlwaysOverwrite:
                                        //no action required
                                        break;

                                    case PropertyMergeRuleActions.OverwriteIfNewer:
                                        if (targetEntity.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated)) > changeSet.Timestamp)
                                        {
                                            changeSet.Data.RemoveProperty(prop.Key);
                                        }
                                        break;

                                    case PropertyMergeRuleActions.OverwriteIfEmpty:
                                        if (!IsEmptyValue(targetEntity.SelectToken(prop.Key, false)))
                                        {
                                            changeSet.Data.RemoveProperty(prop.Key);
                                        }
                                        break;

                                    case PropertyMergeRuleActions.OverwriteIfNotEmpty:
                                        StripNullUpdates(changeSet, prop.Key);
                                        break;

                                    case PropertyMergeRuleActions.NeverOverwrite:
                                    case PropertyMergeRuleActions.DoNotUpdate:
                                        changeSet.Data.RemoveProperty(prop.Key);
                                        break;

                                    default:
                                        throw new ArgumentOutOfRangeException($"{propertyMergeRule.Action} is invalid");
                                }
                            }
                        }
                    }
                }

                #endregion
                // Nothing becomes accepted until properties, keys and merge metadata
                // have all succeeded. A failure discards the entire staged event.
                var stagedEntries = new List<ChangeTrackingEntry>();
                var candidate = (JObject)targetEntity.DeepClone();
                InitTrackedEntityRequest initialisation = null;
                ChangeTrackingEntry initEntry = null;
                if (changeSet.Data.HasValues)
                {
                    var history = trackingEntityCache.Where(e => e.EntityId == changeSet.EntityId).ToList();
                    initEntry = history.FirstOrDefault(e => e.EntryType == ChangeTrackingEntryTypes.Init);
                    if (initEntry == null)
                    {
                        initEntry = new ChangeTrackingEntry
                        {
                            EntityType = changeSet.EntityType, DataSource = changeSet.DataSource,
                            EntityId = changeSet.EntityId, EntryType = ChangeTrackingEntryTypes.Init,
                            Data = (JObject)targetEntity.DeepClone(),
                            Timestamp = targetEntity.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated))
                        };
                        history.Add(initEntry);
                        initialisation = new InitTrackedEntityRequest
                        {
                            EntityType = initEntry.EntityType, DataSource = initEntry.DataSource,
                            EntityData = (JObject)initEntry.Data.DeepClone(), EntityId = initEntry.EntityId,
                            Timestamp = initEntry.Timestamp
                        };
                    }
                    if (changeSet.Timestamp < initEntry.Timestamp) continue;

                    var current = (await mediator.TrySend(new MaterializeDataHubEntityRequest
                    {
                        TrackingEntries = history, EntityType = changeSet.EntityType,
                        EntityId = changeSet.EntityId, SkipSave = true
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response };
                    changeSet.Data = MergeBatchIntegrity.PrepareChange(current.ResultingEntity, changeSet.Data);
                    history.Add(changeSet);
                    var materialized = (await mediator.TrySend(new MaterializeDataHubEntityRequest
                    {
                        TrackingEntries = history, EntityType = changeSet.EntityType,
                        EntityId = changeSet.EntityId, SkipSave = true
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response };
                    var acceptedDiff = ChangeTrackingHelper.StripBaseProperties(
                        ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(current.ResultingEntity, materialized.ResultingEntity) as JObject);
                    if (acceptedDiff?.HasValues == true)
                    {
                        changeSet.Data = acceptedDiff;
                        stagedEntries.Add(changeSet);
                        candidate = materialized.ResultingEntity;
                    }
                }

                if (!doNotProcessAltKeys && hasAlternateKeyChanges)
                {
                    if (initialisation == null && !trackingEntityCache.Any(e => e.EntityId == changeSet.EntityId && e.EntryType == ChangeTrackingEntryTypes.Init))
                        throw new InvalidOperationException("Destination change history requires an initialization checkpoint before merging alternate keys.");
                    var beforeKeys = candidate.DeepClone();
                    AddSourceEntityToDataHubEntityAlternateKeys(request, sourceChange, candidate);
                    CalculateAndAddDataHubEntityChangeTracking(stagedEntries, beforeKeys, candidate,
                        new EntityReference { EntityType = changeSet.EntityType, EntityId = changeSet.EntityId }, changeSet.Timestamp);
                }
                if (stagedEntries.Count == 0) continue;

                var alternateKeys = candidate.Value<JArray>(nameof(DataHubEntity.alternateKeys))!.ToObject<List<AlternateKey>>();
                var sourceReference = resolvedEntityIdMap[changeSet.EntityId];
                var sourceKey = alternateKeys.FirstOrDefault(k => k.Key == $"{sourceReference.DataSource}.{sourceReference.SourceEntityType}".ToLower());
                if (sourceKey != null) sourceKey.LastMerge = timeService.Now();
                candidate[nameof(DataHubEntity.alternateKeys)] = JArray.FromObject(alternateKeys, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore });

                // History reconstruction returns a source change time. Keep the newer
                // stored freshness proxy for subsequent comparisons in this batch.
                var freshness = targetEntity.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated));
                if (candidate.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated)) < freshness)
                    candidate[nameof(DataHubEntity.lastUpdated)] = freshness;

                if (initialisation != null)
                {
                    _ = (await mediator.SendAsync(initialisation, cancellationToken)) switch
                    { { IsT1: true } response => throw response.AsT1, { AsT0: var response } => response };
                    trackingEntityCache.Add(initEntry);
                }
                trackingEntityCache.AddRange(stagedEntries);
                acceptedChangeTrackingEntries.AddRange(stagedEntries);
                workingEntities[changeSet.EntityId] = candidate;
                updatedDataHubEntities[changeSet.EntityId] = candidate;
            }
            catch (Exception ex)
            {
                MarkSkippedDestination(request, changeSet, skippedDestinationFields);
                ExcludeSourceChange(request, changeSet);
                AddMergeFailedResult(request, results, changeSet.EntityId, resolvedEntityIdMap, ex);
            }
        }
        return updatedDataHubEntities;
    }

    private static void MarkSkippedDestination(ProcessUpdatedEntitiesRequest request, ChangeTrackingEntry entry, Dictionary<string, JObject> skippedFields)
    {
        if (!request.SourceChangesByTrackingEntry.TryGetValue(entry, out var sourceChange)) return;
        if (!skippedFields.TryGetValue(sourceChange.SourceEntityId, out var mask))
            skippedFields[sourceChange.SourceEntityId] = mask = new JObject();
        MergeBatchIntegrity.AccumulateChangedFields(mask, sourceChange.ChangeSet);
    }

    private static void ExcludeSourceChange(ProcessUpdatedEntitiesRequest request, ChangeTrackingEntry entry)
    {
        if (request.SourceChangesByTrackingEntry.TryGetValue(entry, out var sourceChange))
            sourceChange.ExcludeFromSourceHistory = true;
    }

    private async Task<List<ChangeTrackingEntry>> DataHubEntityTrackingEntries(ProcessUpdatedEntitiesRequest request, List<string> dataHubEntityIds)
    {
        var dataHubEntityTrackingEntries = new List<ChangeTrackingEntry>();

        var getDataHubEntityTrackingEntriesResponse = (await mediator.TrySend(new GetTrackingEntriesQuery()
        {
            DataSource = DataSources.DataHub,
            EntityType = request.DataHubEntityType,
            EntityIds = dataHubEntityIds,
            PageSize = 1000
        }, CancellationToken.None)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };


        dataHubEntityTrackingEntries.AddRange(getDataHubEntityTrackingEntriesResponse.Results);

        while (getDataHubEntityTrackingEntriesResponse.MoreResultsAvailable)
        {
            getDataHubEntityTrackingEntriesResponse = (await mediator.TrySend(new GetTrackingEntriesQuery()
            {
                DataSource = DataSources.DataHub,
                EntityType = request.DataHubEntityType,
                EntityIds = dataHubEntityIds,
                PageSize = 1000,
                ContinuationToken = getDataHubEntityTrackingEntriesResponse.ContinuationToken
            }, CancellationToken.None)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            dataHubEntityTrackingEntries.AddRange(getDataHubEntityTrackingEntriesResponse.Results);
        }

        return dataHubEntityTrackingEntries;
    }

    private MergeRule MergeRules(EntityConfig entityConfig, string dataSource, string sourceEntityType)
    {
        var result = entityConfig?.MergeRules?
            .Where(x => (string.Equals(x.DataSource, dataSource, StringComparison.CurrentCultureIgnoreCase) || x.DataSource == "*")
                        && (string.Equals(x.SourceEntityType, sourceEntityType, StringComparison.CurrentCultureIgnoreCase) || x.SourceEntityType == "*")
                        && (string.Equals(x.Context, MergeContexts.MergeUpdatedEntity, StringComparison.CurrentCultureIgnoreCase) || x.Context == "*" || string.IsNullOrEmpty(x.Context)))
            .OrderByDescending(x => SpecificityScore(x, dataSource, sourceEntityType, MergeContexts.MergeUpdatedEntity))
            .FirstOrDefault();
        return result;
    }

    private static int SpecificityScore(MergeRule rule, string dataSource, string sourceEntityType, string context)
    {
        var score = 0;
        if (string.Equals(rule.DataSource, dataSource, StringComparison.CurrentCultureIgnoreCase)) score += 4;
        if (string.Equals(rule.SourceEntityType, sourceEntityType, StringComparison.CurrentCultureIgnoreCase)) score += 2;
        if (string.Equals(rule.Context, context, StringComparison.CurrentCultureIgnoreCase)) score += 1;
        return score;
    }

    private static bool IsEmptyValue(JToken token)
    {
        if (token == null || token.Type is JTokenType.Null or JTokenType.Undefined) return true;

        if (token.Type == JTokenType.String) return string.IsNullOrEmpty(token.Value<string>());
        if (token is JArray array) return array.Count == 0;
        if (token is JObject obj) return !obj.Properties().Any();

        if (token is JValue valueToken)
        {
            var value = valueToken.Value;
            return value != null && value.Equals(Activator.CreateInstance(value.GetType()));
        }

        return false;
    }

    private (List<MergeEntityResult> results, List<string> dataHubEntityIds, Dictionary<string, ExternalEntityReference> resolvedEntityIdMap) InitializeVars(ProcessUpdatedEntitiesRequest request)
    {
        var results = new List<MergeEntityResult>();

        var dataHubEntityIds = request.ConvertedSourceEntityChanges
            .Select(s => s.EntityId)
            .Concat(request.SourceEntityChangesWithAlternateKeys.Select(s => request.ResolvedReferencedEntities.First(f => f.SourceEntityReference.EntityId == s.SourceEntityId).DataHubEntityReference.EntityId))
            .Distinct()
            .ToList();

        var resolvedEntityIdMap = request.ResolvedReferencedEntities.GroupBy(r => r.DataHubEntityReference.EntityId)
            .ToDictionary(g => g.Key, g => g.First().SourceEntityReference);


        return (results, dataHubEntityIds, resolvedEntityIdMap);
    }

    private void StripNullUpdates(ChangeTrackingEntry changeSet, string propPath)
    {
        var prop = changeSet.Data.SelectToken(propPath);
        if (prop == null) return;

        if (prop.Type == JTokenType.Array)
        {
            var arrVal = (JArray)prop;
            if (arrVal.HasValues)
            {
                prop = arrVal.Count == 1 ? arrVal[0] : arrVal[1];
            }
        }

        if (prop.Type == JTokenType.Null)
        {
            var p = changeSet.Data.SelectToken(propPath);
            p?.Parent?.Remove();
        }

        if (prop.Type == JTokenType.Object)
        {
            foreach (var child in prop.ToList())
            {
                StripNullUpdates(changeSet, child.Path);
            }

            if (!prop.HasValues)
            {
                var p = changeSet.Data.SelectToken(prop.Path);
                p?.Parent?.Remove();
            }
        }
    }

    #endregion
}
