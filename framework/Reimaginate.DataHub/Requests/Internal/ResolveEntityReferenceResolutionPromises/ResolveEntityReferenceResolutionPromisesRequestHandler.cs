using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceResolutionPromises;

public class ResolveEntityReferenceResolutionPromisesRequestHandler(IMediator mediator) : IHandler<ResolveEntityReferenceResolutionPromisesRequest, ResolveEntityReferenceResolutionPromisesResponse>
{
    public ResolveEntityReferenceResolutionPromisesRequestHandler(IMediator mediator, ITimeService timeService)
        : this(mediator)
    {
    }

    public async Task<ResolveEntityReferenceResolutionPromisesResponse> HandleAsync(ResolveEntityReferenceResolutionPromisesRequest request, CancellationToken cancellationToken)
    {
        var sourceSystemEntityIds = request.SourceSystemEntityIds;
        var resolutionPromisesToResolve = new List<ResolutionPromise>();
        var updatedEntities = new List<JObject>();
            
        #region Get resolution promises

        var resolutionPromiseParameters = new List<QueryParameter>();
        var sourceSystemEntityIdParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(sourceSystemEntityIds, "sourceSystemEntityId", resolutionPromiseParameters);
        var resolutionPromiseWhereClause = $"x.{nameof(ResolutionPromise.ExternalEntityReference)}.{nameof(ResolutionPromise.ExternalEntityReference.EntityId)} in ({string.Join(",", sourceSystemEntityIdParameterNames)})";

        var getResolutionPromisesResponse = (await mediator.TrySend( new GetCosmosDocumentsQuery<ResolutionPromise>()
        {
            WhereClause = resolutionPromiseWhereClause,
            PageSize = 1000,
            Parameters = resolutionPromiseParameters
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        resolutionPromisesToResolve.AddRange(getResolutionPromisesResponse.Results);

        while (getResolutionPromisesResponse.MoreResultsAvailable)
        {
            getResolutionPromisesResponse = (await mediator.TrySend( new GetCosmosDocumentsQuery<ResolutionPromise>()
            {
                WhereClause = resolutionPromiseWhereClause,
                PageSize = 1000,
                ContinuationToken = getResolutionPromisesResponse.ContinuationToken,
                Parameters = resolutionPromiseParameters
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            resolutionPromisesToResolve.AddRange(getResolutionPromisesResponse.Results);
        }

        #endregion

        if (resolutionPromisesToResolve.Any())
        {
            var resolutionPromisesToDelete = new List<ResolutionPromise>();

            var dataHubEntitiesToUpdate = new Dictionary<(string EntityType, string EntityId), JObject>();
            var changeSetsToAdd = new List<AddTrackedEntityChangeSetRequest>();

            var resolutionPromiseByDataHubEntityType = resolutionPromisesToResolve.GroupBy(g => g.DataHubEntityType);
            foreach (var entityTypeGroup in resolutionPromiseByDataHubEntityType)
            {
                var dataHubEntityType = entityTypeGroup.Key;
                var dataHubEntityIdsToResolve = entityTypeGroup.Select(s => s.DataHubEntityId).Distinct().ToList();
                    
                var getDataHubEntitiesByIdResponse = (await mediator.TrySend( new GetDataHubEntitiesByIdRequest()
                {
                    EntityType = dataHubEntityType,
                    EntityIds = dataHubEntityIdsToResolve
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                var dataHubEntitiesToResolve = getDataHubEntitiesByIdResponse.Results;
                    
                foreach (var resolutionPromise in entityTypeGroup)
                {
                    var dataHubEntityToUpdate = dataHubEntitiesToResolve.FirstOrDefault(f => f[nameof(DataHubEntity.entityType)].Value<string>() == dataHubEntityType && f[nameof(DataHubEntity.id)]!.Value<string>() == resolutionPromise.DataHubEntityId);
                    if (dataHubEntityToUpdate == null)
                    {
                        resolutionPromisesToDelete.Add(resolutionPromise);
                        continue;
                    }

                    var entityKey = (dataHubEntityType, resolutionPromise.DataHubEntityId);
                    if (!dataHubEntitiesToUpdate.TryGetValue(entityKey, out var updatedDataHubEntity))
                    {
                        updatedDataHubEntity = (JObject)dataHubEntityToUpdate.DeepClone();
                    }

                    var preResolutionDataHubEntity = (JObject)updatedDataHubEntity.DeepClone();
                    var updatedDataHubEntityId = updatedDataHubEntity[nameof(DataHubEntity.id)]!.Value<string>();
                    var updatedDataHubEntityType = updatedDataHubEntity[nameof(DataHubEntity.entityType)]!.Value<string>();

                    var entityReferenceToResolve = updatedDataHubEntity.SelectToken(resolutionPromise.EntityReferencePath);
                    if (entityReferenceToResolve == null) throw new Exception("Entity reference to resolve not found");

                    if (!IsCurrentReferenceMatchingPromise(entityReferenceToResolve, resolutionPromise))
                    {
                        if (!EntityContainsMatchingExternalReference(updatedDataHubEntity, resolutionPromise))
                        {
                            resolutionPromisesToDelete.Add(resolutionPromise);
                        }

                        continue;
                    }

                    var resolvedEntity = request.ResolvedReferencedEntities.FirstOrDefault(f => IsMatchingResolvedEntity(f, resolutionPromise));

                    if (resolvedEntity != null)
                    {
                        if (!dataHubEntitiesToUpdate.ContainsKey(entityKey))
                        {
                            dataHubEntitiesToUpdate.Add(entityKey, updatedDataHubEntity);
                        }

                        var resolvedEntityId = resolvedEntity.DataHubEntityReference.EntityId;
                        var resolvedEntityType = resolvedEntity.DataHubEntityReference.EntityType;

                        entityReferenceToResolve.Replace(JObject.FromObject(new EntityReference()
                        {
                            EntityId = resolvedEntityId,
                            EntityType = resolvedEntityType
                        }));
                        
                        var changeSet = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(preResolutionDataHubEntity, updatedDataHubEntity);

                        var changeTrackingRequest = new AddTrackedEntityChangeSetRequest()
                        {
                            DataSource = DataSources.DataHub,
                            EntityType = updatedDataHubEntityType,
                            SourceEntityId = updatedDataHubEntityId,
                            TimeStamp = resolutionPromise.ExternalEntityReference.Timestamp ?? updatedDataHubEntity.DataHubLastUpdated(),
                            ChangeSet = ChangeTrackingHelper.StripBaseProperties(changeSet)
                        };
                        changeSetsToAdd.Add(changeTrackingRequest);

                        resolutionPromisesToDelete.Add(resolutionPromise);
                    }
                }
            }

            #region Commit changes to Cosmos DB


            if (changeSetsToAdd.Any() && !request.DoNotTrack)
            {
                _ = (await mediator.TrySend( new AddTrackedEntityChangeSetsRequest()
                {
                    Requests = changeSetsToAdd
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            }

            if (dataHubEntitiesToUpdate.Any())
            {
                _ = (await mediator.TrySend( new UpsertDataHubEntitiesCommand()
                {
                    Entities = dataHubEntitiesToUpdate.Values.Select(s => s.RemoveNullValues()).ToList()
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                updatedEntities.AddRange(dataHubEntitiesToUpdate.Values);
            }

            if (resolutionPromisesToDelete.Any())
            {
                _ = (await mediator.SendAsync( new DeleteCosmosDocumentsCommand<ResolutionPromise>()
                {
                    Documents = resolutionPromisesToDelete.DistinctBy(p => p.id).ToList()
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
            }

            #endregion

        }

        return new ResolveEntityReferenceResolutionPromisesResponse()
        {
            UpdatedDataHubEntities = updatedEntities
        };
    }

    private static bool IsMatchingResolvedEntity(ResolvedEntityReference resolvedEntity, ResolutionPromise resolutionPromise)
    {
        return string.Equals(resolvedEntity.DataHubEntityReference.EntityType, resolutionPromise.ExternalEntityReference.EntityType, StringComparison.Ordinal)
               && string.Equals(resolvedEntity.SourceEntityReference.DataSource, resolutionPromise.ExternalEntityReference.DataSource, StringComparison.OrdinalIgnoreCase)
               && string.Equals(resolvedEntity.SourceEntityReference.SourceEntityType, resolutionPromise.ExternalEntityReference.SourceEntityType, StringComparison.OrdinalIgnoreCase)
               && string.Equals(resolvedEntity.SourceEntityReference.EntityId, resolutionPromise.ExternalEntityReference.EntityId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCurrentReferenceMatchingPromise(JToken entityReferenceToResolve, ResolutionPromise resolutionPromise)
    {
        if (entityReferenceToResolve is not JObject entityReferenceObject ||
            entityReferenceObject.Value<string>("@Tag") != nameof(ExternalEntityReference))
        {
            return false;
        }

        var currentExternalReference = entityReferenceObject.ToObjectIgnoreErrors<ExternalEntityReference>();
        return IsMatchingExternalReference(currentExternalReference, resolutionPromise.ExternalEntityReference);
    }

    private static bool EntityContainsMatchingExternalReference(JObject dataHubEntity, ResolutionPromise resolutionPromise)
    {
        return dataHubEntity
            .ExternalEntityReferences()
            .Select(reference => reference.ToObjectIgnoreErrors<ExternalEntityReference>())
            .Any(reference => IsMatchingExternalReference(reference, resolutionPromise.ExternalEntityReference));
    }

    private static bool IsMatchingExternalReference(ExternalEntityReference currentReference, ExternalEntityReference expectedReference)
    {
        return currentReference != null
               && expectedReference != null
               && string.Equals(currentReference.EntityType, expectedReference.EntityType, StringComparison.Ordinal)
               && string.Equals(currentReference.DataSource, expectedReference.DataSource, StringComparison.OrdinalIgnoreCase)
               && string.Equals(currentReference.SourceEntityType, expectedReference.SourceEntityType, StringComparison.OrdinalIgnoreCase)
               && string.Equals(currentReference.EntityId, expectedReference.EntityId, StringComparison.OrdinalIgnoreCase);
    }
}
