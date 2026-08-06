using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.FindMatchingEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceLookups;

internal sealed record ResolveEntityReferenceLookup(
    string LookupId,
    string EntityType,
    string AlternateKey,
    string Value);

internal sealed record ResolveEntityReferenceLookupResult(
    string LookupId,
    IReadOnlyList<EntityReference> Matches);

internal sealed class ResolveEntityReferenceLookupsProcessor(IMediator mediator)
{
    private const int BatchSize = 500;

    public async Task<IReadOnlyList<ResolveEntityReferenceLookupResult>> ProcessAsync(
        IReadOnlyCollection<ResolveEntityReferenceLookup> lookups,
        CancellationToken cancellationToken)
    {
        if (lookups.Count == 0)
        {
            return [];
        }

        var orderedLookups = lookups.ToList();
        var matchesByLookupId = orderedLookups.ToDictionary(
            lookup => lookup.LookupId,
            _ => new List<EntityReference>(),
            StringComparer.Ordinal);

        foreach (var lookupGroup in orderedLookups.GroupBy(lookup => new LookupGroupKey(lookup.EntityType, lookup.AlternateKey)))
        {
            var groupLookups = lookupGroup.ToList();
            var values = groupLookups
                .Select(lookup => lookup.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            for (var index = 0; index < values.Count; index += BatchSize)
            {
                var valueBatch = values.Skip(index).Take(BatchSize).ToList();
                var parameters = new List<QueryParameter>
                {
                    new("alternateKey", lookupGroup.Key.AlternateKey)
                };
                var valueParameterNames = valueBatch.Select((value, parameterIndex) =>
                {
                    var name = $"entityId{parameterIndex}";
                    parameters.Add(new QueryParameter(name, value));
                    return $"@{name}";
                }).ToList();

                var response = (await mediator.TrySend(new FindMatchingEntitiesQuery
                {
                    EntityType = lookupGroup.Key.EntityType,
                    WhereClause = $"exists (select ak from ak in x.{nameof(DataHubEntity.alternateKeys)} where ak['Key'] = @alternateKey and ak['Value'] in ({string.Join(",", valueParameterNames)}))",
                    SelectClause = $"x.{nameof(DataHubEntity.id)},x.{nameof(DataHubEntity.entityType)},x.{nameof(DataHubEntity.alternateKeys)}",
                    Parameters = parameters
                }, cancellationToken)) switch
                {
                    { Item2: { } exception } => throw exception,
                    { Item1: var value } => value
                };

                foreach (var matchingEntity in response.OfType<JObject>())
                {
                    var matchingValues = matchingEntity[nameof(DataHubEntity.alternateKeys)]
                        ?.ToObject<List<AlternateKey>>()
                        ?.Where(alternateKey => string.Equals(alternateKey.Key, lookupGroup.Key.AlternateKey, StringComparison.Ordinal))
                        .Select(alternateKey => alternateKey.Value)
                        .ToHashSet(StringComparer.Ordinal) ?? [];

                    if (matchingValues.Count == 0)
                    {
                        continue;
                    }

                    var resolvedReference = new EntityReference
                    {
                        EntityType = matchingEntity.Value<string>(nameof(DataHubEntity.entityType)),
                        EntityId = matchingEntity.Value<string>(nameof(DataHubEntity.id))
                    };

                    foreach (var lookup in groupLookups.Where(lookup => matchingValues.Contains(lookup.Value)))
                    {
                        var matches = matchesByLookupId[lookup.LookupId];
                        if (matches.All(match =>
                                !string.Equals(match.EntityType, resolvedReference.EntityType, StringComparison.Ordinal) ||
                                !string.Equals(match.EntityId, resolvedReference.EntityId, StringComparison.Ordinal)))
                        {
                            matches.Add(resolvedReference);
                        }
                    }
                }
            }
        }

        return orderedLookups
            .Select(lookup => new ResolveEntityReferenceLookupResult(lookup.LookupId, matchesByLookupId[lookup.LookupId]))
            .ToList();
    }

    private sealed record LookupGroupKey(string EntityType, string AlternateKey);
}
