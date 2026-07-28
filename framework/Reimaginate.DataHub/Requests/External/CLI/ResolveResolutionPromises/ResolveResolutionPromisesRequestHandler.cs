using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.FindEntitiesByAlternateKey;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.ResolveResolutionPromises;

public class ResolveResolutionPromisesRequestHandler(IMediator mediator) : IHandler<ResolveResolutionPromisesRequest, ResolveResolutionPromisesResponse>
{
    private const string ResolvedStatus = "Resolved";
    private const string UnresolvedStatus = "Unresolved";
    private const string StaleStatus = "Stale";
    private const string FailedStatus = "Failed";
    private const string MultipleAlternateKeyMatchesReason = "Multiple entities found with matching alternate keys";

    public async Task<ResolveResolutionPromisesResponse> HandleAsync(ResolveResolutionPromisesRequest request, CancellationToken cancellationToken)
    {
        var query = CreatePromiseQuery(request);
        var processedPromiseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetLookupCache = new Dictionary<TargetLookupKey, JArray>();
        var results = new List<ResolveResolutionPromiseResult>();
        var page = await LoadPromisePage(query, request.PageSize, request.ContinuationToken, cancellationToken);
        var pagePromises = (page.Results ?? [])
            .Where(promise => !string.IsNullOrWhiteSpace(promise.id))
            .Where(promise => processedPromiseIds.Add(promise.id))
            .ToList();

        if (pagePromises.Any())
        {
            var pageResult = await ProcessPromisePage(request, pagePromises, targetLookupCache, cancellationToken);
            results.AddRange(pageResult.Results);
            await CommitPage(request, pageResult, cancellationToken);
        }

        return new ResolveResolutionPromisesResponse
        {
            MatchedCount = results.Count,
            ResolvedCount = results.Count(result => result.Status == ResolvedStatus),
            UnresolvedCount = results.Count(result => result.Status == UnresolvedStatus),
            DeletedStaleCount = results.Count(result => result.Status == StaleStatus),
            FailedCount = results.Count(result => result.Status == FailedStatus),
            ContinuationToken = page.ContinuationToken,
            MoreResultsAvailable = page.MoreResultsAvailable,
            Results = results
        };
    }

    private async Task<PageProcessingResult> ProcessPromisePage(
        ResolveResolutionPromisesRequest request,
        List<ResolutionPromise> promises,
        Dictionary<TargetLookupKey, JArray> targetLookupCache,
        CancellationToken cancellationToken)
    {
        var ownerEntities = await LoadOwnerEntities(promises, cancellationToken);
        var ownerPatches = new Dictionary<(string EntityType, string EntityId), OwnerPatch>();
        var promisesToDelete = new List<ResolutionPromise>();
        var results = new List<ResolveResolutionPromiseResult>();

        foreach (var promise in promises)
        {
            await ProcessPromise(request, promise, ownerEntities, ownerPatches, promisesToDelete, results, targetLookupCache, cancellationToken);
        }

        return new PageProcessingResult(results, ownerPatches, promisesToDelete);
    }

    private async Task CommitPage(ResolveResolutionPromisesRequest request, PageProcessingResult pageResult, CancellationToken cancellationToken)
    {
        if (!request.DryRun)
        {
            if (pageResult.OwnerPatches.Any())
            {
                var patchResponse = (await mediator.TrySend(new ProcessPatchEntitiesRequest
                {
                    CorrelationId = request.CorrelationId,
                    DoNotTrack = request.DoNotTrack,
                    Requests = pageResult.OwnerPatches.Values.Select(ToPatchRequest).ToList()
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var value } => value };

                var patchResults = patchResponse.Results ?? [];
                if (patchResults.Count != pageResult.OwnerPatches.Count)
                {
                    throw new DataHubException(
                        DataHubErrorCategory.ServerError,
                        "Patch pipeline did not return a result for every resolved reference owner.",
                        nameof(ResolveResolutionPromisesRequest),
                        request.CorrelationId,
                        pageResult.OwnerPatches.Values.Select(ownerPatch => new DataHubErrorDetail
                        {
                            Field = "Owner",
                            Code = "PatchResultExpected",
                            Message = $"{ownerPatch.EntityType}:{ownerPatch.EntityId}"
                        }).ToList());
                }

                var failedPatch = patchResults.FirstOrDefault(result => !result.Success);
                if (failedPatch != null)
                {
                    throw new DataHubException(
                        DataHubErrorCategory.ServerError,
                        $"Failed to patch resolved reference owner {failedPatch.RequestId}: {failedPatch.FailureReason}",
                        nameof(ResolveResolutionPromisesRequest),
                        request.CorrelationId,
                        new[]
                        {
                            new DataHubErrorDetail
                            {
                                Field = "Owner",
                                Code = "PatchFailed",
                                Message = failedPatch.RequestId
                            },
                            new DataHubErrorDetail
                            {
                                Field = "FailureReason",
                                Code = "PatchFailureReason",
                                Message = failedPatch.FailureReason
                            }
                        });
                }
            }

            if (pageResult.PromisesToDelete.Any())
            {
                _ = (await mediator.SendAsync(new DeleteCosmosDocumentsCommand<ResolutionPromise>
                {
                    Documents = pageResult.PromisesToDelete.DistinctBy(promise => promise.id).ToList()
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var value } => value };
            }
        }
    }

    private async Task ProcessPromise(
        ResolveResolutionPromisesRequest request,
        ResolutionPromise promise,
        IReadOnlyDictionary<(string EntityType, string EntityId), JObject> ownerEntities,
        Dictionary<(string EntityType, string EntityId), OwnerPatch> ownerPatches,
        List<ResolutionPromise> promisesToDelete,
        List<ResolveResolutionPromiseResult> results,
        Dictionary<TargetLookupKey, JArray> targetLookupCache,
        CancellationToken cancellationToken)
    {
        var result = CreateResult(promise);
        results.Add(result);

        var ownerKey = (promise.DataHubEntityType, promise.DataHubEntityId);
        if (!ownerEntities.TryGetValue(ownerKey, out var storedOwnerEntity))
        {
            MarkStale(result, "Owner entity was not found.");
            promisesToDelete.Add(promise);
            return;
        }

        if (!ownerPatches.TryGetValue(ownerKey, out var ownerPatch))
        {
            ownerPatch = new OwnerPatch(
                promise.DataHubEntityType,
                promise.DataHubEntityId,
                (JObject)storedOwnerEntity.DeepClone(),
                []);
        }

        var referenceToken = ownerPatch.Entity.SelectToken(promise.EntityReferencePath, false);
        if (!IsCurrentReferenceMatchingPromise(referenceToken, promise))
        {
            if (!EntityContainsMatchingExternalReference(ownerPatch.Entity, promise))
            {
                MarkStale(result, "Matching external reference no longer exists on the owner entity.");
                promisesToDelete.Add(promise);
                return;
            }

            MarkUnresolved(result, "Matching external reference was not found at the promised path.");
            return;
        }

        var foundEntities = await FindResolvedTarget(promise, targetLookupCache, cancellationToken);
        if (!foundEntities.Any())
        {
            MarkUnresolved(result, "No DataHub entity matched the external reference alternate key.");
            return;
        }

        if (foundEntities.Count > 1)
        {
            if (request.StopOnFailure)
            {
                throw CreateMultipleAlternateKeyMatchesException(request, promise, foundEntities.Count);
            }

            MarkFailed(result, MultipleAlternateKeyMatchesReason);
            return;
        }

        var foundEntity = foundEntities.First();
        var resolvedEntity = new EntityReference
        {
            EntityId = foundEntity.DataHubEntityId(),
            EntityType = foundEntity.DataHubEntityType()
        };

        var resolvedReferenceToken = JObject.FromObject(resolvedEntity, new JsonSerializer { DefaultValueHandling = DefaultValueHandling.Ignore });
        referenceToken!.Replace(resolvedReferenceToken.DeepClone());
        ownerPatch.Operations.Add(new Patch
        {
            Operation = "set",
            Path = promise.EntityReferencePath,
            Value = resolvedReferenceToken
        });
        ownerPatches[ownerKey] = ownerPatch;
        promisesToDelete.Add(promise);

        result.Status = ResolvedStatus;
        result.Reason = "Resolved by alternate key.";
        result.ResolvedEntityType = resolvedEntity.EntityType;
        result.ResolvedEntityId = resolvedEntity.EntityId;
    }

    private PromiseQuery CreatePromiseQuery(ResolveResolutionPromisesRequest request)
    {
        var parameters = new List<QueryParameter>();
        string whereClause;

        if (request.PromiseIds is { Count: > 0 })
        {
            var idParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(request.PromiseIds, "promiseId", parameters);
            whereClause = $"x.{nameof(ResolutionPromise.id)} in ({string.Join(",", idParameterNames)})";
        }
        else
        {
            whereClause = request.WhereClause;
            parameters.AddRange(DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters));
        }

        return new PromiseQuery(whereClause, parameters);
    }

    private async Task<PagedResults<ResolutionPromise>> LoadPromisePage(PromiseQuery query, int pageSize, string continuationToken, CancellationToken cancellationToken)
    {
        return (await mediator.TrySend(new GetCosmosDocumentsQuery<ResolutionPromise>
        {
            WhereClause = query.WhereClause,
            PageSize = pageSize,
            ContinuationToken = continuationToken,
            Parameters = query.Parameters
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var value } => value };
    }

    private async Task<Dictionary<(string EntityType, string EntityId), JObject>> LoadOwnerEntities(List<ResolutionPromise> promises, CancellationToken cancellationToken)
    {
        var owners = new Dictionary<(string EntityType, string EntityId), JObject>();
        foreach (var typeGroup in promises.GroupBy(promise => promise.DataHubEntityType))
        {
            var response = (await mediator.TrySend(new GetDataHubEntitiesByIdRequest
            {
                EntityType = typeGroup.Key,
                EntityIds = typeGroup.Select(promise => promise.DataHubEntityId).Distinct().ToList()
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var value } => value };

            foreach (var entity in response.Results ?? [])
            {
                owners[(entity.DataHubEntityType(), entity.DataHubEntityId())] = entity;
            }
        }

        return owners;
    }

    private async Task<JArray> FindResolvedTarget(
        ResolutionPromise promise,
        Dictionary<TargetLookupKey, JArray> targetLookupCache,
        CancellationToken cancellationToken)
    {
        var lookupKey = CreateTargetLookupKey(promise);

        if (targetLookupCache.TryGetValue(lookupKey, out var cachedResult))
        {
            return cachedResult;
        }

        var result = (await mediator.TrySend(new FindEntitiesByAlternateKeyRequest
        {
            EntityType = lookupKey.EntityType,
            Key = lookupKey.Key,
            Value = lookupKey.Value
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var value } => value };

        targetLookupCache[lookupKey] = result;
        return result;
    }

    private static DataHubInvalidRequestException CreateMultipleAlternateKeyMatchesException(
        ResolveResolutionPromisesRequest request,
        ResolutionPromise promise,
        int matchCount)
    {
        var lookupKey = CreateTargetLookupKey(promise);
        return new DataHubInvalidRequestException(
            MultipleAlternateKeyMatchesReason,
            nameof(ResolveResolutionPromisesRequest),
            request.CorrelationId,
            new[]
            {
                new DataHubErrorDetail
                {
                    Field = nameof(ResolutionPromise.id),
                    Code = "ResolutionPromise",
                    Message = promise.id
                },
                new DataHubErrorDetail
                {
                    Field = "Owner",
                    Code = promise.DataHubEntityType,
                    Message = promise.DataHubEntityId
                },
                new DataHubErrorDetail
                {
                    Field = nameof(ResolutionPromise.EntityReferencePath),
                    Code = "ReferencePath",
                    Message = promise.EntityReferencePath
                },
                new DataHubErrorDetail
                {
                    Field = "AlternateKey",
                    Code = "DuplicateMatch",
                    Message = $"{lookupKey.EntityType}:{lookupKey.Key}={lookupKey.Value} matched {matchCount} entities."
                }
            });
    }

    private static TargetLookupKey CreateTargetLookupKey(ResolutionPromise promise)
    {
        return new TargetLookupKey(
            promise.ExternalEntityReference.EntityType,
            $"{promise.ExternalEntityReference.DataSource}.{promise.ExternalEntityReference.SourceEntityType}".ToLowerInvariant(),
            promise.ExternalEntityReference.EntityId);
    }

    private static ResolveResolutionPromiseResult CreateResult(ResolutionPromise promise)
    {
        var externalReference = promise.ExternalEntityReference;
        return new ResolveResolutionPromiseResult
        {
            PromiseId = promise.id,
            DataHubEntityType = promise.DataHubEntityType,
            DataHubEntityId = promise.DataHubEntityId,
            EntityReferencePath = promise.EntityReferencePath,
            DataSource = externalReference?.DataSource,
            SourceEntityType = externalReference?.SourceEntityType,
            SourceEntityId = externalReference?.EntityId,
            TargetEntityType = externalReference?.EntityType,
            Status = UnresolvedStatus
        };
    }

    private static ProcessPatchEntityRequest ToPatchRequest(OwnerPatch ownerPatch)
    {
        return new ProcessPatchEntityRequest
        {
            RequestId = $"{ownerPatch.EntityType}:{ownerPatch.EntityId}",
            DataSource = DataSources.DataHub,
            EntityType = ownerPatch.EntityType,
            EntityId = ownerPatch.EntityId,
            Operations = ownerPatch.Operations
        };
    }

    private static void MarkUnresolved(ResolveResolutionPromiseResult result, string reason)
    {
        result.Status = UnresolvedStatus;
        result.Reason = reason;
    }

    private static void MarkStale(ResolveResolutionPromiseResult result, string reason)
    {
        result.Status = StaleStatus;
        result.Reason = reason;
    }

    private static void MarkFailed(ResolveResolutionPromiseResult result, string reason)
    {
        result.Status = FailedStatus;
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

    private sealed record PromiseQuery(string WhereClause, IReadOnlyCollection<QueryParameter> Parameters);

    private sealed record TargetLookupKey(string EntityType, string Key, string Value);

    private sealed record OwnerPatch(string EntityType, string EntityId, JObject Entity, List<Patch> Operations);

    private sealed record PageProcessingResult(
        List<ResolveResolutionPromiseResult> Results,
        Dictionary<(string EntityType, string EntityId), OwnerPatch> OwnerPatches,
        List<ResolutionPromise> PromisesToDelete);
}
