using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessResolutionPromises;

internal enum ResolutionPromiseProcessingStatus
{
    Resolved,
    Unresolved,
    Stale,
    Failed
}

internal sealed class ProcessResolutionPromisesOptions
{
    public bool DryRun { get; init; }
    public bool DoNotTrack { get; init; }
    public bool ThrowWhenReferencePathMissing { get; init; }
    public Func<ResolutionPromise, int, Exception> MultipleMatchExceptionFactory { get; init; }
}

internal sealed class ResolutionPromiseProcessingResult
{
    public ResolutionPromise Promise { get; init; }
    public ResolutionPromiseProcessingStatus Status { get; set; } = ResolutionPromiseProcessingStatus.Unresolved;
    public string Reason { get; set; }
    public EntityReference ResolvedReference { get; set; }
}

internal sealed class ProcessResolutionPromisesResponse
{
    public List<ResolutionPromiseProcessingResult> Results { get; init; } = [];
    public List<JObject> UpdatedDataHubEntities { get; init; } = [];
}

internal sealed class ProcessResolutionPromisesProcessor(IMediator mediator)
{
    private const int CommitBatchSize = 100;
    private const int DeleteBatchSize = 500;
    private const string MultipleAlternateKeyMatchesReason = "Multiple entities found with matching alternate keys";

    public async Task<ProcessResolutionPromisesResponse> ProcessAsync(
        IReadOnlyCollection<ResolutionPromise> promises,
        Func<IReadOnlyCollection<ResolutionPromise>, CancellationToken, Task<IReadOnlyDictionary<string, IReadOnlyList<EntityReference>>>> resolveTargets,
        ProcessResolutionPromisesOptions options,
        CancellationToken cancellationToken)
    {
        var promiseList = promises.ToList();
        if (promiseList.Count == 0)
        {
            return new ProcessResolutionPromisesResponse();
        }

        var ownerEntities = await LoadOwnerEntities(promiseList, cancellationToken);
        var actionablePromises = new List<ResolutionPromise>();

        foreach (var promise in promiseList)
        {
            if (!ownerEntities.TryGetValue((promise.DataHubEntityType, promise.DataHubEntityId), out var ownerEntity))
            {
                continue;
            }

            var referenceToken = ownerEntity.SelectToken(promise.EntityReferencePath, false);
            if (referenceToken == null && options.ThrowWhenReferencePathMissing)
            {
                throw new Exception("Entity reference to resolve not found");
            }

            if (IsCurrentReferenceMatchingPromise(referenceToken, promise))
            {
                actionablePromises.Add(promise);
            }
        }

        var targets = actionablePromises.Count == 0
            ? new Dictionary<string, IReadOnlyList<EntityReference>>(StringComparer.OrdinalIgnoreCase)
            : await resolveTargets(actionablePromises, cancellationToken);
        var updatedEntities = new Dictionary<(string EntityType, string EntityId), JObject>();
        var changeSetsToAdd = new List<AddTrackedEntityChangeSetRequest>();
        var promisesToDelete = new List<ResolutionPromise>();
        var results = new List<ResolutionPromiseProcessingResult>();

        foreach (var promise in promiseList)
        {
            var result = new ResolutionPromiseProcessingResult { Promise = promise };
            results.Add(result);

            var ownerKey = (promise.DataHubEntityType, promise.DataHubEntityId);
            if (!ownerEntities.TryGetValue(ownerKey, out var storedOwnerEntity))
            {
                MarkStale(result, "Owner entity was not found.");
                promisesToDelete.Add(promise);
                continue;
            }

            var hasWorkingOwner = updatedEntities.TryGetValue(ownerKey, out var ownerEntity);
            ownerEntity ??= storedOwnerEntity;

            var referenceToken = ownerEntity.SelectToken(promise.EntityReferencePath, false);
            if (referenceToken == null && options.ThrowWhenReferencePathMissing)
            {
                throw new Exception("Entity reference to resolve not found");
            }

            if (!IsCurrentReferenceMatchingPromise(referenceToken, promise))
            {
                if (!EntityContainsMatchingExternalReference(ownerEntity, promise))
                {
                    MarkStale(result, "Matching external reference no longer exists on the owner entity.");
                    promisesToDelete.Add(promise);
                }
                else
                {
                    MarkUnresolved(result, "Matching external reference was not found at the promised path.");
                }

                continue;
            }

            var resolvedTargets = targets.TryGetValue(promise.id, out var matches)
                ? matches
                : [];
            if (resolvedTargets.Count == 0)
            {
                MarkUnresolved(result, "No DataHub entity matched the external reference alternate key.");
                continue;
            }

            if (resolvedTargets.Count > 1)
            {
                if (options.MultipleMatchExceptionFactory != null)
                {
                    throw options.MultipleMatchExceptionFactory(promise, resolvedTargets.Count);
                }

                result.Status = ResolutionPromiseProcessingStatus.Failed;
                result.Reason = MultipleAlternateKeyMatchesReason;
                continue;
            }

            var resolvedTarget = resolvedTargets[0];
            var resolvedReference = new EntityReference
            {
                EntityId = resolvedTarget.EntityId,
                EntityType = resolvedTarget.EntityType
            };

            if (!hasWorkingOwner)
            {
                ownerEntity = (JObject)storedOwnerEntity.DeepClone();
                referenceToken = ownerEntity.SelectToken(promise.EntityReferencePath, false);
            }

            var preResolutionEntity = (JObject)ownerEntity.DeepClone();
            referenceToken!.Replace(JObject.FromObject(resolvedReference, new JsonSerializer
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            }));

            updatedEntities[ownerKey] = ownerEntity;
            promisesToDelete.Add(promise);
            result.Status = ResolutionPromiseProcessingStatus.Resolved;
            result.Reason = "Resolved by alternate key.";
            result.ResolvedReference = resolvedReference;

            var changeSet = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(preResolutionEntity, ownerEntity);
            changeSetsToAdd.Add(new AddTrackedEntityChangeSetRequest
            {
                DataSource = DataSources.DataHub,
                EntityType = ownerEntity.DataHubEntityType(),
                SourceEntityId = ownerEntity.DataHubEntityId(),
                TimeStamp = promise.ExternalEntityReference.Timestamp ?? ownerEntity.DataHubLastUpdated(),
                ChangeSet = ChangeTrackingHelper.StripBaseProperties(changeSet)
            });
        }

        if (!options.DryRun)
        {
            if (changeSetsToAdd.Count > 0 && !options.DoNotTrack)
            {
                foreach (var changeSetBatch in changeSetsToAdd.Chunk(CommitBatchSize))
                {
                    var trackingResponse = (await mediator.TrySend(new AddTrackedEntityChangeSetsRequest
                    {
                        Requests = changeSetBatch.ToList()
                    }, cancellationToken)) switch
                    {
                        { Item2: { } exception } => throw exception,
                        { Item1: var value } => value
                    };

                    if (trackingResponse.Failures?.Count > 0)
                    {
                        throw new AggregateException(trackingResponse.Failures.Select(failure => failure.Error));
                    }
                }
            }

            if (updatedEntities.Count > 0)
            {
                foreach (var entityBatch in updatedEntities.Values.Chunk(CommitBatchSize))
                {
                    var upsertResponse = (await mediator.TrySend(new UpsertDataHubEntitiesCommand
                    {
                        Entities = entityBatch.Select(entity => entity.RemoveNullValues()).ToList()
                    }, cancellationToken)) switch
                    {
                        { Item2: { } exception } => throw exception,
                        { Item1: var value } => value
                    };

                    if (upsertResponse.Failures?.Count > 0)
                    {
                        throw new AggregateException(upsertResponse.Failures.Select(failure => failure.Error));
                    }
                }
            }

            if (promisesToDelete.Count > 0)
            {
                foreach (var promiseBatch in promisesToDelete.DistinctBy(promise => promise.id).Chunk(DeleteBatchSize))
                {
                    var deleteResponse = (await mediator.SendAsync(new DeleteCosmosDocumentsCommand<ResolutionPromise>
                    {
                        Documents = promiseBatch.ToList()
                    }, cancellationToken)) switch
                    {
                        { IsT1: true } result => throw result.AsT1,
                        { AsT0: var value } => value
                    };

                    var deleteFailures = (deleteResponse.Failures ?? [])
                        .Where(failure => failure?.Error != null && !IsAlreadyDeletedFailure(failure.Error))
                        .ToList();
                    if (deleteFailures.Count > 0)
                    {
                        throw new AggregateException(deleteFailures.Select(failure => failure.Error));
                    }
                }
            }
        }

        return new ProcessResolutionPromisesResponse
        {
            Results = results,
            UpdatedDataHubEntities = updatedEntities.Values.ToList()
        };
    }

    private async Task<Dictionary<(string EntityType, string EntityId), JObject>> LoadOwnerEntities(
        IReadOnlyCollection<ResolutionPromise> promises,
        CancellationToken cancellationToken)
    {
        var owners = new Dictionary<(string EntityType, string EntityId), JObject>();
        foreach (var typeGroup in promises.GroupBy(promise => promise.DataHubEntityType))
        {
            var response = (await mediator.TrySend(new GetDataHubEntitiesByIdRequest
            {
                EntityType = typeGroup.Key,
                EntityIds = typeGroup.Select(promise => promise.DataHubEntityId).Distinct().ToList()
            }, cancellationToken)) switch
            {
                { Item2: { } exception } => throw exception,
                { Item1: var value } => value
            };

            foreach (var entity in response.Results ?? [])
            {
                owners[(entity.DataHubEntityType(), entity.DataHubEntityId())] = entity;
            }
        }

        return owners;
    }

    private static void MarkUnresolved(ResolutionPromiseProcessingResult result, string reason)
    {
        result.Status = ResolutionPromiseProcessingStatus.Unresolved;
        result.Reason = reason;
    }

    private static void MarkStale(ResolutionPromiseProcessingResult result, string reason)
    {
        result.Status = ResolutionPromiseProcessingStatus.Stale;
        result.Reason = reason;
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

    private static bool IsAlreadyDeletedFailure(Exception exception)
    {
        return exception switch
        {
            CosmosException { StatusCode: HttpStatusCode.NotFound } => true,
            { Message: { } message } when message.Contains("not found", StringComparison.OrdinalIgnoreCase) => true,
            { Message: { } message } when message.Contains("404", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }
}
