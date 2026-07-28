using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.CreateDeferredEntityResolutionPromises;

public class CreateDeferredEntityResolutionPromisesRequestHandler(IMediator mediator) : IHandler<CreateDeferredEntityResolutionPromisesRequest, CreateDeferredEntityResolutionPromisesResponse>
{
    public async Task<CreateDeferredEntityResolutionPromisesResponse> HandleAsync(CreateDeferredEntityResolutionPromisesRequest request, CancellationToken cancellationToken)
    {
        var unresolvedEntityRefs = request.Entities.Select(entity => new
        {
            entity,
            tokens = JObject.FromObject(entity).Descendants().OfType<JObject>()
                .Where(w => w.ContainsKey("@Tag") && w.Value<string>("@Tag") == nameof(ExternalEntityReference)).Select(entityRef => new { path = entityRef.Path, entityRef = entityRef.ToObject<ExternalEntityReference>() }).ToList()
        }).ToList();

        var resolutionPromises = new List<ResolutionPromise>();
        unresolvedEntityRefs.ForEach(unresolvedEntityRef =>
        {
            resolutionPromises.AddRange(unresolvedEntityRef.tokens.Select(token =>
            {
                var dataHubEntityType = unresolvedEntityRef.entity[nameof(DataHubEntity.entityType)]!.Value<string>();
                var dataHubEntityId = unresolvedEntityRef.entity[nameof(DataHubEntity.id)]!.Value<string>();

                return new ResolutionPromise()
                {
                    id = $"{token.entityRef.SourceEntityType}:{token.entityRef.EntityId}->{token.path}:{dataHubEntityType}:{dataHubEntityId}",
                    DataHubEntityType = dataHubEntityType,
                    DataHubEntityId = dataHubEntityId,
                    EntityReferencePath = token.path,
                    ExternalEntityReference = token.entityRef
                };
            }));
        });

        _ = (await mediator.SendAsync( new UpsertCosmosDocumentsCommand<ResolutionPromise>()
        {
            Documents = resolutionPromises
        }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };

        return new CreateDeferredEntityResolutionPromisesResponse()
        {
            ResultingPromises = resolutionPromises
        };
    }
}