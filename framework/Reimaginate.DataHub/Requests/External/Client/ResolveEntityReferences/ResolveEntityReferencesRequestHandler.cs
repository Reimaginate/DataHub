using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.FindMatchingEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.ResolveEntityReferences;

public class ResolveEntityReferencesRequestHandler(IMediator mediator) : IHandler<ResolveEntityReferencesRequest, ResolveEntityReferencesResponse>
{
    public async Task<ResolveEntityReferencesResponse> HandleAsync(ResolveEntityReferencesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var entityTypeGroups = request.EntityReferences.GroupBy(g => new { g.DataSource, g.EntityType, g.SourceEntityType });
            var failures = new List<ResolveEntityReferenceException>();

            var ret = new Dictionary<ExternalEntityReference, EntityReference>();

            foreach (var entityTypeGroup in entityTypeGroups)
            {
                var key = $"{entityTypeGroup.Key.DataSource}.{entityTypeGroup.Key.SourceEntityType}".ToLower();
                var entityRefs = entityTypeGroup.DistinctBy(d => d.EntityId).ToList();

                while (entityRefs.Any())
                {
                    var batch = entityRefs.Take(5000).ToList();
                    var parameters = new List<QueryParameter>
                    {
                        new("alternateKey", key)
                    };
                    var entityIdParameterNames = batch.Select((entityRef, index) =>
                    {
                        var name = $"entityId{index}";
                        parameters.Add(new QueryParameter(name, entityRef.EntityId));
                        return $"@{name}";
                    });

                    var matchWhereClause = $"exists (select ak from ak in x.{nameof(DataHubEntity.alternateKeys)} where ak['Key'] = @alternateKey and ak['Value'] in ({string.Join(",", entityIdParameterNames)}))";
                    var selectClause = $"x.{nameof(DataHubEntity.id)},x.{nameof(DataHubEntity.entityType)},x.{nameof(DataHubEntity.alternateKeys)}";
                    var findMatchingEntitiesQuery = new FindMatchingEntitiesQuery()
                    {
                        EntityType = entityTypeGroup.Key.EntityType,
                        WhereClause = matchWhereClause,
                        SelectClause = selectClause,
                        Parameters = parameters
                    };

                    var response = (await mediator.TrySend(findMatchingEntitiesQuery, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                    foreach (var matchingEntity in response)
                    {
                        if (matchingEntity.IsEmpty()) continue;

                        var matchingEntityAlternateKeys = matchingEntity[nameof(DataHubEntity.alternateKeys)]!.ToObject<List<AlternateKey>>();

                        var sourceSystemIds = matchingEntityAlternateKeys.Where(w => w.Key == key).Select(s => s.Value).ToList();
                        var sourceEntityRef = entityRefs.First(f => sourceSystemIds.Contains(f.EntityId));

                        if (ret.ContainsKey(sourceEntityRef))
                        {
                            failures.Add(new ResolveEntityReferenceException(sourceEntityRef));
                            continue;
                        }

                        var newEntityReference = new ExternalEntityReference()
                        {
                            EntityType = matchingEntity.Value<string>(nameof(DataHubEntity.entityType)),
                            EntityId = matchingEntity.DataHubEntityId(),
                            _tag = null
                        };

                        ret.Add(sourceEntityRef, newEntityReference);
                    }

                    entityRefs.RemoveRange(0, batch.Count);
                }
            }

            return new ResolveEntityReferencesResponse()
            {
                Success = true,
                Results = ret.Select(s => new ResolvedEntityReference()
                {
                    SourceEntityReference = s.Key,
                    DataHubEntityReference = s.Value
                }).ToList(),
                ResolutionFailures = failures
            };
        }
        catch (Exception ex)
        {
            return new ResolveEntityReferencesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
