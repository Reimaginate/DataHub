using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataHub.SharedModels.Rules;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdatedUntrackedEntities;

public class ProcessUpdatedUntrackedEntitiesRequestHandler(IMediator mediator, IProcessingLockService processingLockService, ITimeService timeService)
    : IHandler<ProcessUpdatedUntrackedEntitiesRequest, ProcessUpdatedUntrackedEntitiesResponse>
{
    public async Task<ProcessUpdatedUntrackedEntitiesResponse> HandleAsync(ProcessUpdatedUntrackedEntitiesRequest request, CancellationToken cancellationToken)
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

            var updatedDataHubEntities = await CalculateUpdatedDataHubEntities(request, currentDataHubEntities, results, cancellationToken);

            await CommitUpdatedDataHubEntitiesToDb(updatedDataHubEntities, request, resolvedEntityIdMap, cancellationToken, results);

            return new ProcessUpdatedUntrackedEntitiesResponse()
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

    private async Task CommitUpdatedDataHubEntitiesToDb(List<JObject> updatedDataHubEntities, ProcessUpdatedUntrackedEntitiesRequest request, Dictionary<string, ExternalEntityReference> resolvedEntityIdMap, CancellationToken cancellationToken, List<MergeEntityResult> results)
    {
        if (updatedDataHubEntities.Any())
        {
            updatedDataHubEntities.ForEach(e => { e[nameof(DataHubEntity.lastUpdated)] = timeService.Now(); });

            _ = (await mediator.SendAsync(new UpsertDataHubEntitiesCommand()
            {
                Entities = updatedDataHubEntities.Select(s => s.RemoveNullValues()).ToList()
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

            results.AddRange(updatedDataHubEntities.Select(s =>
            {
                var dataHubEntityId = s.DataHubEntityId();
                var sourceEntityId = resolvedEntityIdMap[dataHubEntityId].EntityId;
                return new MergeEntityResult()
                {
                    DataSource = request.DataSource,
                    SourceEntityType = request.SourceEntityType,
                    SourceEntityId = sourceEntityId,
                    DataHubEntityType = request.DataHubEntityType,
                    MergeOutcome = MergeOutcomes.EntityMatchedAndUpdated,
                    DataHubEntityId = dataHubEntityId,
                    ResultingDataHubEntity = s,
                    ResultingDataHubEntityUpdates = s
                };
            }));
        }
    }

    private async Task<List<JObject>> CalculateUpdatedDataHubEntities(ProcessUpdatedUntrackedEntitiesRequest request, List<JObject> dataHubEntities, List<MergeEntityResult> results, CancellationToken cancellationToken)
    {
        var updatedDataHubEntities = new List<JObject>();

        foreach (var mergeRequest in request.MergeRequests)
        {
            var resolvedReference = request.ResolvedReferencedEntities.First(f =>
                f.SourceEntityReference.EntityId == mergeRequest.SourceEntityId &&
                f.SourceEntityReference.SourceEntityType == mergeRequest.SourceEntityType &&
                f.SourceEntityReference.DataSource == mergeRequest.DataSource);

            try
            {
                var targetEntity = dataHubEntities.First(w => w.DataHubEntityId() == resolvedReference.DataHubEntityReference.EntityId);
                var updatedEntity = (JObject)targetEntity.DeepClone();

                var mergeData = (JObject)mergeRequest.Data.DeepClone();

                if (request.EntityConfig != null)
                {
                    var checkResponse = (await mediator.TrySend(new CheckPreMergeRulesRequest
                    {
                        DataSource = request.DataSource,
                        EntityConfig = request.EntityConfig,
                        DataHubEntityType = request.DataHubEntityType,
                        SourceEntityType = request.SourceEntityType,
                        IncomingEntity = mergeRequest.Data,
                        ExistingEntity = targetEntity,
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
                                    MergeOutcome = MergeOutcomes.MergeFailed,
                                    DataHubEntityId = resolvedReference.DataHubEntityReference.EntityId,
                                    SourceEntityId = mergeRequest.SourceEntityId,
                                    FailureReason = "Merge was rejected by pre-merge rules"
                                });

                                continue;

                            case PreMergeRuleActions.SilentlyRejectMerge:
                                results.Add(new MergeEntityResult()
                                {
                                    DataSource = request.DataSource,
                                    SourceEntityType = request.SourceEntityType,
                                    DataHubEntityType = request.DataHubEntityType,
                                    MergeOutcome = MergeOutcomes.MergeRejected,
                                    DataHubEntityId = resolvedReference.DataHubEntityReference.EntityId,
                                    SourceEntityId = mergeRequest.SourceEntityId
                                });

                                continue;

                            case PreMergeRuleActions.AllowMerge:
                                break;

                            default:
                                throw new Exception("Invalid pre-merge rule action");
                        }
                    }

                    var mergeRules = MergeRules(request.EntityConfig, request.DataSource, request.SourceEntityType);
                    if (mergeRules != null)
                    {
                        var protectedProps = typeof(DataHubEntityBase).GetProperties().Select(s => s.Name).ToList();

                        var props = mergeData.GetAllProperties();

                        foreach (var prop in props)
                        {
                            var propertyMergeRule = mergeRules.Rules.FirstOrDefault(w => string.Equals(w.PropertyName, prop.Key, StringComparison.CurrentCultureIgnoreCase) || w.PropertyName == "*");
                            if (propertyMergeRule != null)
                            {
                                switch (propertyMergeRule.Action)
                                {
                                    case PropertyMergeRuleActions.AlwaysOverwrite:
                                        //no action required
                                        break;

                                    case PropertyMergeRuleActions.OverwriteIfNewer:
                                        break;

                                    case PropertyMergeRuleActions.OverwriteIfEmpty:
                                        //TODO
                                        break;

                                    case PropertyMergeRuleActions.OverwriteIfNotEmpty:
                                        //StripNullUpdates(mergeData, prop.Key);
                                        break;

                                    case PropertyMergeRuleActions.NeverOverwrite:
                                    case PropertyMergeRuleActions.DoNotUpdate:
                                        mergeData.RemoveProperty(prop.Key);
                                        break;

                                    default:
                                        throw new ArgumentOutOfRangeException($"{propertyMergeRule.Action} is invalid");
                                }
                            }
                        }

                        protectedProps.ForEach(prop =>
                        {
                            mergeData.Remove(prop);
                        });
                    }
                }

                updatedEntity.Merge(mergeData, new JsonMergeSettings()
                {
                    MergeArrayHandling = MergeArrayHandling.Replace,
                    MergeNullValueHandling = MergeNullValueHandling.Merge
                });

                var updateDataHubEntityId = targetEntity.DataHubEntityId()!;
                updatedEntity[nameof(DataHubEntity.id)].Replace(updateDataHubEntityId);

                var currentDataHubAlternateKeys = targetEntity.Value<JArray>(nameof(DataHubEntity.alternateKeys))?.ToObject<List<AlternateKey>>();

                var sourceEntityAltKey = currentDataHubAlternateKeys!.FirstOrDefault(f => f.Key == $"{mergeRequest.DataSource}.{mergeRequest.SourceEntityType}".ToLower());
                if (sourceEntityAltKey != null) sourceEntityAltKey.LastMerge = timeService.Now();

                updatedEntity[nameof(DataHubEntity.alternateKeys)] = JArray.FromObject(currentDataHubAlternateKeys, new JsonSerializer() { NullValueHandling = NullValueHandling.Ignore });
                updatedEntity[nameof(DataHubEntity.entityType)] = mergeRequest.DataHubEntityType;

                updatedDataHubEntities.Add(updatedEntity);
            }
            catch (Exception ex)
            {
                results.Add(new MergeEntityResult()
                {
                    DataSource = request.DataSource,
                    SourceEntityType = request.SourceEntityType,
                    DataHubEntityType = request.DataHubEntityType,
                    MergeOutcome = MergeOutcomes.MergeFailed,
                    DataHubEntityId = resolvedReference.DataHubEntityReference.EntityId,
                    SourceEntityId = mergeRequest.SourceEntityId,
                    FailureReason = ex.Message
                });
            }
        }

        return updatedDataHubEntities;
    }

    private MergeRule MergeRules(EntityConfig entityConfig, string dataSource, string sourceEntityType)
    {
        var result = entityConfig.MergeRules?
            .FirstOrDefault(x => (x.DataSource == dataSource || x.DataSource == "*")
                                 && (x.SourceEntityType == sourceEntityType || x.SourceEntityType == "*")
                                 && (x.Context == MergeContexts.MergeUpdatedEntity || x.Context == "*"));
        return result;
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

    private (List<MergeEntityResult> results, List<string> dataHubEntityIds, Dictionary<string, ExternalEntityReference> resolvedEntityIdMap) InitializeVars(ProcessUpdatedUntrackedEntitiesRequest request)
    {
        var results = new List<MergeEntityResult>();

        var dataHubEntityIds = request.MergeRequests
            .Select(mergeRequest => request.ResolvedReferencedEntities.First(f =>
                f.SourceEntityReference.EntityId == mergeRequest.SourceEntityId &&
                f.SourceEntityReference.SourceEntityType == mergeRequest.SourceEntityType &&
                f.SourceEntityReference.DataSource == mergeRequest.DataSource).DataHubEntityReference.EntityId)
            .ToList();

        var resolvedEntityIdMap = request.ResolvedReferencedEntities.ToDictionary(k => k.DataHubEntityReference.EntityId, v => v.SourceEntityReference);

        return (results, dataHubEntityIds, resolvedEntityIdMap);
    }

    #endregion
}
