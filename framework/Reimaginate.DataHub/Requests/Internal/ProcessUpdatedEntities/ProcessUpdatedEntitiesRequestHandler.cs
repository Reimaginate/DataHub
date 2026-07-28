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

            var updatedDataHubEntities = await CalculateUpdatedDataHubEntities(request, currentDataHubEntities, currentDataHubEntityChangeTrackingEntries, results, resolvedEntityIdMap, cancellationToken);

            var mergeRules = MergeRules(request.EntityConfig, request.DataSource, request.SourceEntityType);
            var doNotProcessAltKeys = mergeRules?.Rules.Any(a => a.PropertyName == nameof(DataHubEntity.alternateKeys) && (a.Action == PropertyMergeRuleActions.DoNotUpdate || a.Action == PropertyMergeRuleActions.NeverOverwrite)) ?? false;

            if (doNotProcessAltKeys == false)
            {
                foreach (var sourceEntityChange in request.SourceEntityChangesWithAlternateKeys)
                {
                    await ProcessAlternateKeyChanges(sourceEntityChange, request, updatedDataHubEntities, currentDataHubEntityChangeTrackingEntries, results, resolvedEntityIdMap, cancellationToken);
                }
            }

            if (!request.DoNotTrack)
            {
                var failedUpdates = results.Where(w => w.MergeOutcome == MergeOutcomes.MergeFailed).Select(s => s.DataHubEntityId).ToList();
                var rejectedUpdates = results.Where(w => w.MergeOutcome == MergeOutcomes.MergeRejected).Select(s => s.DataHubEntityId).ToList();
                var changeTrackingEntries = request.ConvertedSourceEntityChanges.Where(w => !failedUpdates.Contains(w.EntityId) && !rejectedUpdates.Contains(w.EntityId) && w.Data.HasValues).ToList();

                await CommitDataHubEntityChangeTrackingToDb(changeTrackingEntries, cancellationToken);
            }

            await CommitUpdatedDataHubEntitiesToDb(updatedDataHubEntities, request, resolvedEntityIdMap, cancellationToken, results);

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

    private async Task ProcessAlternateKeyChanges(AddTrackedEntityChangeSetRequest sourceEntityChange, ProcessUpdatedEntitiesRequest request, List<JObject> updatedDataHubEntities, List<ChangeTrackingEntry> dataHubEntityTrackingEntries, List<MergeEntityResult> results, Dictionary<string, ExternalEntityReference> resolvedEntityIdMap, CancellationToken cancellationToken)
    {
        var dataHubEntityId = string.Empty;
        try
        {
            var dataHubEntityRef = request.ResolvedReferencedEntities.First(f => f.SourceEntityReference.EntityId == sourceEntityChange.SourceEntityId).DataHubEntityReference;
            dataHubEntityId = dataHubEntityRef.EntityId;

            var updatedDataHubEntity = updatedDataHubEntities.FirstOrDefault(f => f.DataHubEntityId() == dataHubEntityId);

            if (updatedDataHubEntity == null)
            {
                var applicableChanges = request.ConvertedSourceEntityChanges;

                #region Run merge rules

                //TODO ??

                #endregion

                updatedDataHubEntity = await MaterializeUpdatedDataHubEntity(dataHubEntityTrackingEntries, applicableChanges, dataHubEntityRef, cancellationToken);
                updatedDataHubEntities.Add(updatedDataHubEntity);
            }

            var updatedEntityPreChanges = updatedDataHubEntity.DeepClone();

            AddSourceEntityToDataHubEntityAlternateKeys(request, sourceEntityChange, updatedDataHubEntity);

            CalculateAndAddDataHubEntityChangeTracking(request, updatedEntityPreChanges, updatedDataHubEntity, dataHubEntityRef);

        }
        catch (Exception ex)
        {
            AddMergeFailedResult(request, results, dataHubEntityId, resolvedEntityIdMap, ex);
        }
    }

    private async Task<JObject> MaterializeUpdatedDataHubEntity(List<ChangeTrackingEntry> dataHubEntityTrackingEntries, List<ChangeTrackingEntry> applicableChanges, EntityReference dataHubEntityRef, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(
            new MaterializeDataHubEntityRequest()
            {
                TrackingEntries = dataHubEntityTrackingEntries.Concat(applicableChanges).Where(w => w.EntityId == dataHubEntityRef.EntityId).ToList(),
                EntityType = dataHubEntityRef.EntityType,
                EntityId = dataHubEntityRef.EntityId,
                SkipSave = true
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var updatedDataHubEntity = response.ResultingEntity;
        return updatedDataHubEntity;
    }

    private void CalculateAndAddDataHubEntityChangeTracking(ProcessUpdatedEntitiesRequest request, JToken fromEntity, JObject toEntity, EntityReference dataHubEntityRef)
    {
        var changeSet = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(fromEntity, toEntity);

        if (changeSet != null)
        {
            request.ConvertedSourceEntityChanges.Add(new ChangeTrackingEntry()
            {
                id = idService.NewId<ChangeTrackingEntry>(),
                EntryType = ChangeTrackingEntryTypes.Update,
                EntityType = dataHubEntityRef.EntityType,
                EntityId = dataHubEntityRef.EntityId,
                DataSource = DataSources.DataHub,
                Timestamp = toEntity.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated)),
                Data = (JObject)changeSet
            });
        }
    }

    private void AddSourceEntityToDataHubEntityAlternateKeys(ProcessUpdatedEntitiesRequest request, AddTrackedEntityChangeSetRequest sourceEntityChange, JToken updatedDataHubEntity)
    {
        var sourceEntityMergeRequest = request.MergeRequests.First(f => f.SourceEntityId == sourceEntityChange.SourceEntityId);
        var sourceEntityAlternateKeys = sourceEntityMergeRequest.Data.Value<JArray>(nameof(DataHubEntity.alternateKeys));
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

    private async Task CommitUpdatedDataHubEntitiesToDb(List<JObject> updatedDataHubEntities, ProcessUpdatedEntitiesRequest request, Dictionary<string, ExternalEntityReference> resolvedEntityIdMap, CancellationToken cancellationToken, List<MergeEntityResult> results)
    {
        if (updatedDataHubEntities.Any())
        {
            updatedDataHubEntities.ForEach(e => { e[nameof(DataHubEntity.lastUpdated)] = timeService.Now(); });

            var upsertResponse = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
            {
                Entities = updatedDataHubEntities.Select(s => s.RemoveNullValues()).ToList()
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (upsertResponse.Failures.Any())
            {
                throw upsertResponse.Failures.First().Error;
            }

            results.AddRange(updatedDataHubEntities.Select(s =>
            {
                var dataHubEntityId = s.DataHubEntityId();
                var sourceEntityId = resolvedEntityIdMap[dataHubEntityId].EntityId;
                var changeSet = request.ConvertedSourceEntityChanges.FirstOrDefault(f => f.EntityId == dataHubEntityId);
                return new MergeEntityResult()
                {
                    DataSource = request.DataSource,
                    SourceEntityType = request.SourceEntityType,
                    SourceEntityId = sourceEntityId,
                    DataHubEntityType = request.DataHubEntityType,
                    MergeOutcome = MergeOutcomes.EntityMatchedAndUpdated,
                    DataHubEntityId = dataHubEntityId,
                    ResultingDataHubEntity = s,
                    ResultingDataHubEntityUpdates = changeSet?.Data
                };
            }));
        }
    }

    private async Task CommitDataHubEntityChangeTrackingToDb(List<ChangeTrackingEntry> changeTrackingEntries, CancellationToken cancellationToken)
    {
        if (!changeTrackingEntries.Any()) return;

        _ = (await mediator.SendAsync(new AddTrackedEntityChangeSetsRequest()
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
    }

    private async Task<List<JObject>> CalculateUpdatedDataHubEntities(ProcessUpdatedEntitiesRequest request, List<JObject> dataHubEntities, List<ChangeTrackingEntry> trackingEntityCache, List<MergeEntityResult> results, Dictionary<string, ExternalEntityReference> resolvedEntityIdMap, CancellationToken cancellationToken)
    {
        var updatedDataHubEntities = new List<JObject>();
        var changeSetsToProcess = new List<ChangeTrackingEntry>(request.ConvertedSourceEntityChanges);
        foreach (var changeSet in changeSetsToProcess)
        {
            try
            {
                var targetEntity = dataHubEntities.First(w => w.DataHubEntityId() == changeSet.EntityId);

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

                    var mergeRules = MergeRules(request.EntityConfig, request.DataSource, request.SourceEntityType);
                    if (mergeRules != null)
                    {
                        var updates = changeSet.Data;

                        var props = updates.GetAllProperties();

                        foreach (var prop in props)
                        {
                            var propertyMergeRule = GetPropertyMergeRule(mergeRules, prop.Key);
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

                if (changeSet.Data.HasValues)
                {
                    var targetEntityChangeTracking = trackingEntityCache.Where(w => w.EntityId == changeSet.EntityId).ToList();

                    var initChangeTrackingEntry = targetEntityChangeTracking.FirstOrDefault(a => a.EntryType == "Init");
                    if (initChangeTrackingEntry == null)
                    {
                        if (targetEntity == null) throw new Exception("Current ResultingEntity Hub Entity is null");

                        var currentEntityTimeStamp = targetEntity.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated));
                        targetEntity.Remove(nameof(DataHubEntity.lastUpdated));
                        targetEntity[nameof(DataHubEntity.lastUpdated)] = currentEntityTimeStamp;

                        initChangeTrackingEntry = new ChangeTrackingEntry()
                        {
                            EntityType = changeSet.EntityType,
                            DataSource = changeSet.DataSource,
                            EntityId = changeSet.EntityId,
                            EntryType = "Init",
                            Data = targetEntity,
                            Timestamp = currentEntityTimeStamp
                        };

                        trackingEntityCache.Add(initChangeTrackingEntry);
                        targetEntityChangeTracking.Add(initChangeTrackingEntry);

                        var initChangeTrackingRequest = new InitTrackedEntityRequest()
                        {
                            EntityType = initChangeTrackingEntry.EntityType,
                            DataSource = initChangeTrackingEntry.DataSource,
                            EntityData = targetEntity,
                            EntityId = initChangeTrackingEntry.EntityId
                        };

                        _ = (await mediator.SendAsync(initChangeTrackingRequest, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
                    }

                    if (changeSet.Timestamp < initChangeTrackingEntry.Timestamp)
                    {
                        request.ConvertedSourceEntityChanges.Remove(changeSet);
                        continue;
                    };

                    var materializeDataHubEntityResponse = (await mediator.TrySend(new MaterializeDataHubEntityRequest()
                    {
                        TrackingEntries = targetEntityChangeTracking,
                        EntityType = changeSet.EntityType,
                        EntityId = changeSet.EntityId,
                        SkipSave = true
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    var dataHubEntityCurrentState = materializeDataHubEntityResponse.ResultingEntity;

                    targetEntityChangeTracking.Add(changeSet);
                    trackingEntityCache.Add(changeSet);

                    materializeDataHubEntityResponse = (await mediator.TrySend(new MaterializeDataHubEntityRequest()
                    {
                        TrackingEntries = targetEntityChangeTracking,
                        EntityType = changeSet.EntityType,
                        EntityId = changeSet.EntityId,
                        SkipSave = true
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    var updatedDataHubEntity = materializeDataHubEntityResponse.ResultingEntity;

                    var changesToProcess = !JToken.DeepEquals(dataHubEntityCurrentState, updatedDataHubEntity);

                    if (changesToProcess)
                    {
                        var updatedDataHubEntityId = updatedDataHubEntity.DataHubEntityId()!;
                        var updatedDataHubAlternateKeys = updatedDataHubEntity.Value<JArray>(nameof(DataHubEntity.alternateKeys))!.ToObject<List<AlternateKey>>();

                        #region Update the last merged timestamp for the source system alternate key

                        var sourceMergeRequest = resolvedEntityIdMap[updatedDataHubEntityId];
                        var sourceEntityAltKey = updatedDataHubAlternateKeys!.FirstOrDefault(f => f.Key == $"{sourceMergeRequest.DataSource}.{sourceMergeRequest.SourceEntityType}".ToLower());
                        if (sourceEntityAltKey != null) sourceEntityAltKey.LastMerge = timeService.Now();
                        dataHubEntityCurrentState[nameof(DataHubEntity.alternateKeys)]!.Replace(JArray.FromObject(updatedDataHubAlternateKeys, new JsonSerializer() { NullValueHandling = NullValueHandling.Ignore }));

                        #endregion

                        updatedDataHubEntities.Add(updatedDataHubEntity);
                    }
                }

            }
            catch (Exception ex)
            {
                results.Add(new MergeEntityResult()
                {
                    DataSource = request.DataSource,
                    SourceEntityType = request.SourceEntityType,
                    DataHubEntityType = request.DataHubEntityType,
                    MergeOutcome = MergeOutcomes.MergeFailed,
                    DataHubEntityId = changeSet.EntityId,
                    SourceEntityId = resolvedEntityIdMap[changeSet.EntityId].EntityId,
                    FailureReason = ex.Message
                });
            }
        }

        return updatedDataHubEntities;
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

    private static PropertyMergeRule GetPropertyMergeRule(MergeRule mergeRules, string propertyName)
    {
        return mergeRules.Rules.FirstOrDefault(w => string.Equals(w.PropertyName, propertyName, StringComparison.CurrentCultureIgnoreCase))
               ?? mergeRules.Rules.FirstOrDefault(w => w.PropertyName == "*");
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

        var resolvedEntityIdMap = request.ResolvedReferencedEntities.ToDictionary(k => k.DataHubEntityReference.EntityId, v => v.SourceEntityReference);


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
