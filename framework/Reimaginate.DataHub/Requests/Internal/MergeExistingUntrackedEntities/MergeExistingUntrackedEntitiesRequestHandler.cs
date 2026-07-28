using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.CreateDeferredEntityResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.ResolveExternalEntityReferences;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdatedUntrackedEntities;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.Mediator;


namespace Reimaginate.DataHub.Requests.Internal.MergeExistingUntrackedEntities;

public class MergeExistingUntrackedEntitiesRequestHandler(IIdService idService, IMediator mediator) : IHandler<MergeExistingUntrackedEntitiesRequest, MergeExistingUntrackedEntitiesResponse>
{
    private readonly IIdService _idService = idService;
    private const int BatchSize = 5000;

    public async Task<MergeExistingUntrackedEntitiesResponse> HandleAsync(MergeExistingUntrackedEntitiesRequest request, CancellationToken cancellationToken)
    {
        var (results, mergeRequests, resolvedReferencedEntities) = InitializeVars(request);

        while (mergeRequests.Any())
        {
            var mergeRequestsBatch = mergeRequests.Take(BatchSize).ToList();
            await ProcessMergeRequestBatch(mergeRequestsBatch, request, results, cancellationToken);
            mergeRequests.RemoveRange(0, mergeRequestsBatch.Count);
        }

        var resultingDataHubEntities = results.Where(w => w.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated).Select(s => s.ResultingDataHubEntity).ToList();

        var dataHubEntitiesToResolveReferences = resultingDataHubEntities.Where(entity => entity
                .ExternalEntityReferences()
                .Any()
        ).ToList();

        #region Resolve External Entity References

        if (dataHubEntitiesToResolveReferences.Any())
        {
            var externalEntityResolutionResponse = (await mediator.TrySend(new ResolveExternalEntityReferencesRequest()
            {
                DataHubEntitiesToResolve = dataHubEntitiesToResolveReferences,
                DoNotTrack = true
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var updatedDataHubEntities = externalEntityResolutionResponse.UpdatedDataHubEntities;
            resultingDataHubEntities.Replace(updatedDataHubEntities, w => w.DataHubEntityId());

            updatedDataHubEntities.ForEach(e =>
            {
                var matchingMergeResult = results.FirstOrDefault(f => f.DataHubEntityId == e.DataHubEntityId());
                if (matchingMergeResult != null)
                {
                    matchingMergeResult.ResultingDataHubEntity = e;
                }
            });

            dataHubEntitiesToResolveReferences = resultingDataHubEntities.Where(entity => entity
                .ExternalEntityReferences()
                .Any()
            ).ToList();
        }

        #endregion

        #region Create deferred entity resolutions for any unresolved entity references

        if (dataHubEntitiesToResolveReferences.Any())
        {
            _ = (await mediator.SendAsync(new CreateDeferredEntityResolutionPromisesRequest()
            {
                Entities = dataHubEntitiesToResolveReferences
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
        }

        #endregion

        #region Resolve deferred entity reference resolutions for newly created ResultingEntity Hub entities referenced by existing ResultingEntity Hub entities

        if (resolvedReferencedEntities.Any())
        {
            _ = (await mediator.SendAsync(new ResolveEntityReferenceResolutionPromisesRequest()
            {
                SourceSystemEntityIds = request.MergeRequests.Select(s => s.SourceEntityId).ToList(),
                ResolvedReferencedEntities = resolvedReferencedEntities,
                DoNotTrack = true
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
        }

        #endregion

        return new MergeExistingUntrackedEntitiesResponse()
        {
            Results = results
        };
    }


    #region Private helpers

    private async Task ProcessMergeRequestBatch(List<MergeEntityRequest> mergeRequests, MergeExistingUntrackedEntitiesRequest request, List<MergeEntityResult> results,  CancellationToken cancellationToken)
    {
        var mergeDataHubEntityUpdatesResponse = (await mediator.TrySend(new ProcessUpdatedUntrackedEntitiesRequest()
        {
            CorrelationId = request.CorrelationId,
            DataHubEntityType = request.DataHubEntityType,
            DataSource = request.DataSource,
            MergeRequests = mergeRequests,
            ResolvedReferencedEntities = request.ResolvedDataHubEntities,
            SourceEntityType = request.SourceEntityType,
            EntityConfig = request.EntityConfig
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        results.AddRange(mergeDataHubEntityUpdatesResponse.Results);

    }
    private static (List<MergeEntityResult> results, List<MergeEntityRequest> mergeRequests, List<ResolvedEntityReference> resolvedReferencedEntities) InitializeVars(MergeExistingUntrackedEntitiesRequest request)
    {
        var results = new List<MergeEntityResult>();
        var mergeRequests = new List<MergeEntityRequest>(request.MergeRequests);
        return (results, mergeRequests, request.ResolvedDataHubEntities);
    }

    #endregion
}
