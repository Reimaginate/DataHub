using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.FindEntitiesByAlternateKey;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ResolveExternalEntityReferences;

public class ResolveExternalEntityReferencesRequestHandler(IMediator mediator) : IHandler<ResolveExternalEntityReferencesRequest, ResolveExternalEntityReferencesRequestResponse>
{
    public async Task<ResolveExternalEntityReferencesRequestResponse> HandleAsync(ResolveExternalEntityReferencesRequest request, CancellationToken cancellationToken)
    {
        var externalEntityReferences = request
            .DataHubEntitiesToResolve
            .SelectMany(entity => entity.ExternalEntityReferences().Select(extEntityRef => new EntityReferenceLocation()
            {
                DataHubEntityType = entity.Value<string>(nameof(DataHubEntity.entityType)),
                DataHubEntityId = entity.Value<string>(nameof(DataHubEntity.id)),
                EntityReferencePath = extEntityRef.Path.StartsWith($"{entity.Path}.") ? extEntityRef.Path[(entity.Path.Length + 1)..] : extEntityRef.Path,
                ExternalEntityReference = extEntityRef.ToObjectIgnoreErrors<ExternalEntityReference>()
            }))
            .ToList();

        if (!externalEntityReferences.Any()) return new ResolveExternalEntityReferencesRequestResponse();

        var updatedDataHubEntities = new List<JObject>();
        var changeSetsToAdd = new List<AddTrackedEntityChangeSetRequest>();

        var entityTypeGroups = externalEntityReferences.GroupBy(g => new { g.ExternalEntityReference.DataSource, g.ExternalEntityReference.EntityType, g.DataHubEntityType });

        foreach (var entityTypeGroup in entityTypeGroups)
        {
            foreach (var extEntityReferenceLocation in entityTypeGroup)
            {
                var externalEntityRef = extEntityReferenceLocation.ExternalEntityReference;

                var foundEntities = (await mediator.TrySend(new FindEntitiesByAlternateKeyRequest()
                {
                    EntityType = externalEntityRef.EntityType,
                    Key = $"{externalEntityRef.DataSource}.{externalEntityRef.SourceEntityType}".ToLower(),
                    Value = externalEntityRef.EntityId
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                if (!foundEntities.Any()) continue;
                if (foundEntities.Count > 1) throw new InvalidOperationException("Multiple entities found with matching alternate keys");


                var foundEntity = foundEntities.First();

                var matchingRequestEntity = updatedDataHubEntities.FirstOrDefault(f => f.DataHubEntityType() == extEntityReferenceLocation.DataHubEntityType && f.DataHubEntityId() == extEntityReferenceLocation.DataHubEntityId) ??
                    request.DataHubEntitiesToResolve.FirstOrDefault(f => f.DataHubEntityType() == extEntityReferenceLocation.DataHubEntityType && f.DataHubEntityId() == extEntityReferenceLocation.DataHubEntityId);

                if (matchingRequestEntity == null) continue;


                var updatedDataHubEntity = (JObject)matchingRequestEntity.DeepClone();

                var entityReferenceToResolve = updatedDataHubEntity.SelectToken(extEntityReferenceLocation.EntityReferencePath);

                entityReferenceToResolve?.Replace(JObject.FromObject(new EntityReference()
                {
                    EntityId = foundEntity.DataHubEntityId(),
                    EntityType = foundEntity.DataHubEntityType()
                }, new JsonSerializer() { DefaultValueHandling = DefaultValueHandling.Ignore }));
                
                var existingEntry = updatedDataHubEntities.FirstOrDefault(f => f.DataHubEntityType() == updatedDataHubEntity.DataHubEntityType() && f.DataHubEntityId() == updatedDataHubEntity.DataHubEntityId());
                if (existingEntry != null) updatedDataHubEntities.Remove(existingEntry);

                updatedDataHubEntities.Add(updatedDataHubEntity);

                var changeSet = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(matchingRequestEntity, updatedDataHubEntity);

                var changeTrackingRequest = new AddTrackedEntityChangeSetRequest()
                {
                    DataSource = DataSources.DataHub,
                    EntityType = updatedDataHubEntity.DataHubEntityType(),
                    SourceEntityId = updatedDataHubEntity.DataHubEntityId(),
                    TimeStamp = externalEntityRef.Timestamp ?? updatedDataHubEntity.DataHubLastUpdated(),
                    ChangeSet = ChangeTrackingHelper.StripBaseProperties(changeSet)
                };
                changeSetsToAdd.Add(changeTrackingRequest);
            }
        }


        #region Commit changes to Cosmos DB


        if (changeSetsToAdd.Any() && !request.DoNotTrack)
        {
            _ = (await mediator.TrySend(new AddTrackedEntityChangeSetsRequest()
            {
                Requests = changeSetsToAdd
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        }

        if (updatedDataHubEntities.Any())
        {
            _ = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
            {
                Entities = updatedDataHubEntities.Select(s => s.RemoveNullValues()).ToList()
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        }

        #endregion

        return new ResolveExternalEntityReferencesRequestResponse()
        {
            UpdatedDataHubEntities = updatedDataHubEntities
        };
    }

}
