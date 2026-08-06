using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Requests.Internal.ProcessResolutionPromises;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Core;
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

    public async Task<ResolveEntityReferenceResolutionPromisesResponse> HandleAsync(
        ResolveEntityReferenceResolutionPromisesRequest request,
        CancellationToken cancellationToken)
    {
        var promises = await LoadPromises(request.SourceSystemEntityIds, cancellationToken);
        var processor = new ProcessResolutionPromisesProcessor(mediator);
        var response = await processor.ProcessAsync(
            promises,
            (actionablePromises, _) => Task.FromResult(ResolveKnownTargets(actionablePromises, request.ResolvedReferencedEntities)),
            new ProcessResolutionPromisesOptions
            {
                DoNotTrack = request.DoNotTrack,
                ThrowWhenReferencePathMissing = true
            },
            cancellationToken);

        return new ResolveEntityReferenceResolutionPromisesResponse
        {
            UpdatedDataHubEntities = response.UpdatedDataHubEntities
        };
    }

    private async Task<List<ResolutionPromise>> LoadPromises(
        IReadOnlyCollection<string> sourceSystemEntityIds,
        CancellationToken cancellationToken)
    {
        var promises = new List<ResolutionPromise>();
        var parameters = new List<QueryParameter>();
        var parameterNames = DataHubQueryParameterMapper.AddIndexedParameters(sourceSystemEntityIds, "sourceSystemEntityId", parameters);
        var whereClause = $"x.{nameof(ResolutionPromise.ExternalEntityReference)}.{nameof(ResolutionPromise.ExternalEntityReference.EntityId)} in ({string.Join(",", parameterNames)})";

        var response = await LoadPromisePage(whereClause, parameters, null, cancellationToken);
        promises.AddRange(response.Results ?? []);

        while (response.MoreResultsAvailable)
        {
            response = await LoadPromisePage(whereClause, parameters, response.ContinuationToken, cancellationToken);
            promises.AddRange(response.Results ?? []);
        }

        return promises;
    }

    private async Task<PagedResults<ResolutionPromise>> LoadPromisePage(
        string whereClause,
        IReadOnlyCollection<QueryParameter> parameters,
        string continuationToken,
        CancellationToken cancellationToken)
    {
        return (await mediator.TrySend(new GetCosmosDocumentsQuery<ResolutionPromise>
        {
            WhereClause = whereClause,
            PageSize = 1000,
            ContinuationToken = continuationToken,
            Parameters = parameters
        }, cancellationToken)) switch
        {
            { Item2: { } exception } => throw exception,
            { Item1: var value } => value
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<EntityReference>> ResolveKnownTargets(
        IReadOnlyCollection<ResolutionPromise> promises,
        IReadOnlyCollection<ResolvedEntityReference> resolvedReferences)
    {
        var targets = new Dictionary<string, IReadOnlyList<EntityReference>>(StringComparer.Ordinal);
        foreach (var promise in promises)
        {
            var resolvedReference = resolvedReferences.FirstOrDefault(candidate => IsMatchingResolvedEntity(candidate, promise));
            targets[promise.id] = resolvedReference == null
                ? []
                : [resolvedReference.DataHubEntityReference];
        }

        return targets;
    }

    private static bool IsMatchingResolvedEntity(ResolvedEntityReference resolvedEntity, ResolutionPromise resolutionPromise)
    {
        return string.Equals(resolvedEntity.DataHubEntityReference.EntityType, resolutionPromise.ExternalEntityReference.EntityType, StringComparison.Ordinal)
               && string.Equals(resolvedEntity.SourceEntityReference.DataSource, resolutionPromise.ExternalEntityReference.DataSource, StringComparison.OrdinalIgnoreCase)
               && string.Equals(resolvedEntity.SourceEntityReference.SourceEntityType, resolutionPromise.ExternalEntityReference.SourceEntityType, StringComparison.OrdinalIgnoreCase)
               && string.Equals(resolvedEntity.SourceEntityReference.EntityId, resolutionPromise.ExternalEntityReference.EntityId, StringComparison.OrdinalIgnoreCase);
    }
}
