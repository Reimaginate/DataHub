using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Requests.Internal.ProcessResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceLookups;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.ResolveResolutionPromises;

public class ResolveResolutionPromisesRequestHandler(IMediator mediator) : IHandler<ResolveResolutionPromisesRequest, ResolveResolutionPromisesResponse>
{
    private const int MaximumPageSize = 500;
    private const string MultipleAlternateKeyMatchesReason = "Multiple entities found with matching alternate keys";

    public async Task<ResolveResolutionPromisesResponse> HandleAsync(
        ResolveResolutionPromisesRequest request,
        CancellationToken cancellationToken)
    {
        var query = CreatePromiseQuery(request);
        var page = await LoadPromisePage(query, Math.Clamp(request.PageSize, 1, MaximumPageSize), request.ContinuationToken, cancellationToken);
        var processedPromiseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var promises = (page.Results ?? [])
            .Where(promise => !string.IsNullOrWhiteSpace(promise.id))
            .Where(promise => processedPromiseIds.Add(promise.id))
            .ToList();
        var processor = new ProcessResolutionPromisesProcessor(mediator);
        var processingResponse = await processor.ProcessAsync(
            promises,
            ResolveTargets,
            new ProcessResolutionPromisesOptions
            {
                DryRun = request.DryRun,
                DoNotTrack = request.DoNotTrack,
                MultipleMatchExceptionFactory = request.StopOnFailure
                    ? (promise, matchCount) => CreateMultipleAlternateKeyMatchesException(request, promise, matchCount)
                    : null
            },
            cancellationToken);
        var results = processingResponse.Results.Select(ToCliResult).ToList();

        return new ResolveResolutionPromisesResponse
        {
            MatchedCount = results.Count,
            ResolvedCount = processingResponse.Results.Count(result => result.Status == ResolutionPromiseProcessingStatus.Resolved),
            UnresolvedCount = processingResponse.Results.Count(result => result.Status == ResolutionPromiseProcessingStatus.Unresolved),
            DeletedStaleCount = processingResponse.Results.Count(result => result.Status == ResolutionPromiseProcessingStatus.Stale),
            FailedCount = processingResponse.Results.Count(result => result.Status == ResolutionPromiseProcessingStatus.Failed),
            ContinuationToken = page.ContinuationToken,
            MoreResultsAvailable = page.MoreResultsAvailable,
            Results = results
        };
    }

    private PromiseQuery CreatePromiseQuery(ResolveResolutionPromisesRequest request)
    {
        var parameters = new List<QueryParameter>();
        string whereClause;

        if (request.PromiseIds is { Count: > 0 })
        {
            var parameterNames = DataHubQueryParameterMapper.AddIndexedParameters(request.PromiseIds, "promiseId", parameters);
            whereClause = $"x.{nameof(ResolutionPromise.id)} in ({string.Join(",", parameterNames)})";
        }
        else
        {
            whereClause = request.WhereClause;
            parameters.AddRange(DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters));
        }

        return new PromiseQuery(whereClause, parameters);
    }

    private async Task<PagedResults<ResolutionPromise>> LoadPromisePage(
        PromiseQuery query,
        int pageSize,
        string continuationToken,
        CancellationToken cancellationToken)
    {
        return (await mediator.TrySend(new GetCosmosDocumentsQuery<ResolutionPromise>
        {
            WhereClause = query.WhereClause,
            PageSize = pageSize,
            ContinuationToken = continuationToken,
            OrderBy = $"x.{nameof(ResolutionPromise.id)}",
            Parameters = query.Parameters
        }, cancellationToken)) switch
        {
            { Item2: { } exception } => throw exception,
            { Item1: var value } => value
        };
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<EntityReference>>> ResolveTargets(
        IReadOnlyCollection<ResolutionPromise> promises,
        CancellationToken cancellationToken)
    {
        var lookups = promises.Select(promise =>
        {
            var lookupKey = CreateTargetLookupKey(promise);
            return new ResolveEntityReferenceLookup(promise.id, lookupKey.EntityType, lookupKey.Key, lookupKey.Value);
        }).ToList();
        var lookupResults = await new ResolveEntityReferenceLookupsProcessor(mediator)
            .ProcessAsync(lookups, cancellationToken);

        return lookupResults.ToDictionary(
            result => result.LookupId,
            result => result.Matches,
            StringComparer.OrdinalIgnoreCase);
    }

    private static ResolveResolutionPromiseResult ToCliResult(ResolutionPromiseProcessingResult processingResult)
    {
        var promise = processingResult.Promise;
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
            ResolvedEntityType = processingResult.ResolvedReference?.EntityType,
            ResolvedEntityId = processingResult.ResolvedReference?.EntityId,
            Status = processingResult.Status.ToString(),
            Reason = processingResult.Reason
        };
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

    private sealed record PromiseQuery(string WhereClause, IReadOnlyCollection<QueryParameter> Parameters);
    private sealed record TargetLookupKey(string EntityType, string Key, string Value);
}
