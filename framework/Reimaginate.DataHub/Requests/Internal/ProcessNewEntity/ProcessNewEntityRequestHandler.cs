using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.Requests.Internal.CreateChangeTrackingEntry;
using Reimaginate.DataHub.Requests.Internal.FindDuplicate;
using Reimaginate.DataHub.Requests.Internal.ProcessNewEntityIntoExistingEntity;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessNewEntity;

public class ProcessNewEntityRequestHandler(IIdService idService, IMediator mediator, ITimeService timeService)
    : IHandler<ProcessNewEntityRequest, ProcessNewEntityResponse>
{
    public async Task<ProcessNewEntityResponse> HandleAsync(ProcessNewEntityRequest request, CancellationToken cancellationToken)
    {
        var changeTrackingEntries = new List<ChangeTrackingEntry>();
        var sourceEntityCreatedOn = request.SourceEntity.DateTimeOffsetValueRequired(nameof(DataHubEntity.createdOn));

        var (mergeRejected, mergeResponse) = await CheckPreMergeRulesAsync(request, MergeContexts.NewEntityPreMerge, request.SourceEntity, null, cancellationToken);
        if (mergeRejected) return mergeResponse;

        if (!request.DoNotTrack)
        {
            var result = await CreateChangeTrackingEntryForSourceEntity(request, sourceEntityCreatedOn, cancellationToken);
            changeTrackingEntries.Add(result);
        }

        EntityWithChangeSet resultingEntity;

        if (CanCheckForDuplicates(request))
        {
            var (duplicateFound, duplicate) = await CheckForDuplicateAsync(request, cancellationToken);
            if (duplicateFound)
            {
                resultingEntity = new EntityWithChangeSet { Entity = duplicate };

                (mergeRejected, mergeResponse) = await CheckPreMergeRulesAsync(request, MergeContexts.MatchedEntityPreMerge, request.SourceEntity, resultingEntity.Entity, cancellationToken);
                if (mergeRejected) return mergeResponse;

                var firstTimeEntityMergeResponse = await MergeEntitiesForTheFirstTimeAsync(request, resultingEntity, cancellationToken, request.DoNotTrack);
                firstTimeEntityMergeResponse.ResultingChangeTrackingEntries = changeTrackingEntries.Concat(firstTimeEntityMergeResponse.ResultingChangeTrackingEntries).ToList();
                return firstTimeEntityMergeResponse;
            }
        }

        (mergeRejected, mergeResponse) = await CheckPreMergeRulesAsync(request, MergeContexts.UnmatchedEntityPreMerge, request.SourceEntity, null, cancellationToken);
        if (mergeRejected) return mergeResponse;

        resultingEntity = InitializeNewDataHubEntity(request, out var newDataHubEntityId);
        AddSourceEntityToAlternateKeys(request, resultingEntity);

        if (!request.DoNotTrack)
        {
            var result = await CreateChangeTrackingEntryForDataHubEntity(request, resultingEntity, newDataHubEntityId, sourceEntityCreatedOn, cancellationToken);
            changeTrackingEntries.Add(result);
        }

        return new ProcessNewEntityResponse
        {
            MergeEntityResult = new MergeEntityResult
            {
                DataSource = request.DataSource,
                SourceEntityType = request.SourceEntityType,
                SourceEntityId = request.SourceEntityId,
                DataHubEntityType = request.DataHubEntityType,
                DataHubEntityId = resultingEntity.Entity.DataHubEntityId()!,
                ResultingDataHubEntity = resultingEntity.Entity,
                ResultingDataHubEntityUpdates = resultingEntity.ChangeSet,
                MergeOutcome = MergeOutcomes.NewEntityCreated,
            },
            ResultingChangeTrackingEntries = changeTrackingEntries
        };
    }


    #region Private helpers

    private void AddSourceEntityToAlternateKeys(ProcessNewEntityRequest request, EntityWithChangeSet resultingEntity)
    {
        var altKeys = JArray.FromObject(new List<AlternateKey>
        {
            new($"{request.DataSource}.{request.SourceEntityType}".ToLower(), request.SourceEntityId)
            {
                LastMerge = timeService.Now()
            }
        });

        var sourceEntityAlternateKeys = request.SourceEntity.Value<JArray>(nameof(DataHubEntity.alternateKeys));
        if (sourceEntityAlternateKeys != null)
        {
            foreach (var sourceEntityAltKey in sourceEntityAlternateKeys.ToList())
            {
                var existingAltKey = altKeys.FirstOrDefault(dataHubEntityAltKey => dataHubEntityAltKey.Value<string>(nameof(AlternateKey.Key)) == sourceEntityAltKey.Value<string>(nameof(AlternateKey.Key)));
                if (existingAltKey == null)
                {
                    altKeys.Add(sourceEntityAltKey);
                    continue;
                }

                existingAltKey[nameof(AlternateKey.Value)] = sourceEntityAltKey[nameof(AlternateKey.Value)];
            }
        }

        resultingEntity.Entity[nameof(DataHubEntity.alternateKeys)] = altKeys;
    }

    private async Task<(bool, ProcessNewEntityResponse)> CheckPreMergeRulesAsync(ProcessNewEntityRequest request, string context, JObject incomingEntity, JObject existingEntity, CancellationToken cancellationToken)
    {
        var checkResponse = (await mediator.TrySend(new CheckPreMergeRulesRequest
        {
            DataSource = request.DataSource,
            EntityConfig = request.EntityConfig,
            DataHubEntityType = request.DataHubEntityType,
            SourceEntityType = request.SourceEntityType,
            Context = context,
            IncomingEntity = incomingEntity,
            ExistingEntity = existingEntity
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (checkResponse.Pass) return (false, null);

        return checkResponse.Action switch
        {
            PreMergeRuleActions.RejectMerge => (true, new ProcessNewEntityResponse
            {
                MergeEntityResult = new MergeEntityResult
                {
                    DataSource = request.DataSource,
                    SourceEntityType = request.SourceEntityType,
                    DataHubEntityType = request.DataHubEntityType,
                    MergeOutcome = MergeOutcomes.MergeRejected,
                    DataHubEntityId = existingEntity?.DataHubEntityId(),
                    SourceEntityId = request.SourceEntityId
                }
            }),
            PreMergeRuleActions.SilentlyRejectMerge => (true, new ProcessNewEntityResponse
            {
                MergeEntityResult = new MergeEntityResult
                {
                    DataSource = request.DataSource,
                    SourceEntityType = request.SourceEntityType,
                    DataHubEntityType = request.DataHubEntityType,
                    MergeOutcome = MergeOutcomes.MergeSilentlyRejected,
                    DataHubEntityId = existingEntity?.DataHubEntityId(),
                    SourceEntityId = request.SourceEntityId
                }
            }),
            _ => throw new ArgumentException($"{checkResponse.Action} is an invalid rule action")
        };
    }

    private bool CanCheckForDuplicates(ProcessNewEntityRequest request)
    {
        return request.DuplicatePreventionRule != null && request.PotentialDuplicates.Any();
    }

    private async Task<ChangeTrackingEntry> CreateChangeTrackingEntryForSourceEntity(ProcessNewEntityRequest request, DateTimeOffset timeStamp, CancellationToken cancellationToken)
    {
        return (await mediator.TrySend(new CreateChangeTrackingEntryRequest()
        {
            EntryType = ChangeTrackingEntryTypes.Init,
            EntityType = request.SourceEntityType,
            Data = ChangeTrackingHelper.StripBaseProperties(request.SourceEntity).RemoveNullValues(),
            EntityId = request.SourceEntityId,
            DataSource = request.DataSource,
            Timestamp = timeStamp,
            SaveNow = false

        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
    }

    private async Task<ChangeTrackingEntry> CreateChangeTrackingEntryForDataHubEntity(ProcessNewEntityRequest request, EntityWithChangeSet resultingEntity, string newDataHubEntityId, DateTimeOffset timeStamp, CancellationToken cancellationToken)
    {
        return (await mediator.TrySend(new CreateChangeTrackingEntryRequest()
        {
            EntryType = ChangeTrackingEntryTypes.Init,
            EntityType = request.DataHubEntityType,
            Data = ChangeTrackingHelper.StripBaseProperties(resultingEntity.Entity).RemoveNullValues(),
            EntityId = newDataHubEntityId,
            DataSource = DataSources.DataHub,
            Timestamp = timeStamp,
            SaveNow = false

        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
    }

    private EntityWithChangeSet InitializeNewDataHubEntity(ProcessNewEntityRequest request, out string newDataHubEntityId)
    {
        newDataHubEntityId = idService.NewId<DataHubEntity>();
        var resultingEntity = new EntityWithChangeSet
        {
            Entity = (JObject)JObject.FromObject(request.SourceEntity).DeepClone()
        };
        resultingEntity.Entity[nameof(DataHubEntity.id)] = newDataHubEntityId;
        resultingEntity.Entity[nameof(DataHubEntity.entityType)] = request.DataHubEntityType;
        return resultingEntity;
    }

    private async Task<ProcessNewEntityResponse> MergeEntitiesForTheFirstTimeAsync(ProcessNewEntityRequest request, EntityWithChangeSet resultingEntity, CancellationToken cancellationToken, bool doNotTrack = false)
    {
        var resultingChangeTrackingEntries = new List<ChangeTrackingEntry>();

        var firstTimeEntityMergeResponse = (await mediator.TrySend(new ProcessNewEntityIntoExistingEntityRequest
        {
            DataSource = request.DataSource,
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = request.SourceEntityId,

            FromEntity = request.SourceEntity,
            ToEntity = resultingEntity.Entity,

            EntityConfig = request.EntityConfig,
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (firstTimeEntityMergeResponse.EntityUpdates != null)
        {
            resultingEntity.Entity = firstTimeEntityMergeResponse.ResultingEntity;
            resultingEntity.ChangeSet = firstTimeEntityMergeResponse.EntityUpdates;

            var resultingEntityLastUpdatedTimestamp = resultingEntity.Entity.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated));

            if (!doNotTrack)
            {
                var changeTrackingEntry = (await mediator.TrySend(new CreateChangeTrackingEntryRequest()
                {
                    EntryType = ChangeTrackingEntryTypes.Update,
                    DataSource = DataSources.DataHub,
                    EntityType = firstTimeEntityMergeResponse.ResultingEntity.Value<string>(nameof(DataHubEntity.entityType)),
                    EntityId = firstTimeEntityMergeResponse.ResultingEntity.DataHubEntityId(),
                    Timestamp = resultingEntityLastUpdatedTimestamp,
                    Data = ChangeTrackingHelper.StripBaseProperties(resultingEntity.ChangeSet),
                    SaveNow = false
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                resultingChangeTrackingEntries.Add(changeTrackingEntry);
            }
        }

        return new ProcessNewEntityResponse
        {
            MergeEntityResult = new MergeEntityResult
            {
                DataSource = request.DataSource,
                SourceEntityType = request.SourceEntityType,
                SourceEntityId = request.SourceEntityId,
                DataHubEntityType = request.DataHubEntityType,
                DataHubEntityId = resultingEntity.Entity.DataHubEntityId()!,
                ResultingDataHubEntity = resultingEntity.Entity,
                ResultingDataHubEntityUpdates = resultingEntity.ChangeSet,
                MergeOutcome = resultingEntity.ChangeSet is { HasValues: true } ? MergeOutcomes.EntityMatchedAndUpdated : MergeOutcomes.EntityMatchedButNotUpdated,
            },
            ResultingChangeTrackingEntries = resultingChangeTrackingEntries
        };
    }

    private async Task<(bool, JObject)> CheckForDuplicateAsync(ProcessNewEntityRequest request, CancellationToken cancellationToken)
    {
        var findDuplicateResponse = (await mediator.TrySend(new FindDuplicateRequest
        {
            DataSource = request.DataSource,
            DataHubEntityType = request.DataHubEntityType,
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = request.SourceEntityId,
            DuplicatePreventionRule = request.DuplicatePreventionRule,
            PotentialDuplicates = request.PotentialDuplicates,
            EntityToMatch = request.SourceEntity
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return findDuplicateResponse.DuplicateEntity != null ? (true, findDuplicateResponse.DuplicateEntity) : (false, null);
    }

    #endregion
}