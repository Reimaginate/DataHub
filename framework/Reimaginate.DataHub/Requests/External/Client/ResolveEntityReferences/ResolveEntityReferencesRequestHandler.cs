using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceLookups;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.ResolveEntityReferences;

public class ResolveEntityReferencesRequestHandler(IMediator mediator) : IHandler<ResolveEntityReferencesRequest, ResolveEntityReferencesResponse>
{
    public async Task<ResolveEntityReferencesResponse> HandleAsync(
        ResolveEntityReferencesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceReferences = request.EntityReferences
                .GroupBy(reference => new { reference.DataSource, reference.EntityType, reference.SourceEntityType })
                .SelectMany(group => group.DistinctBy(reference => reference.EntityId))
                .ToList();
            var lookups = sourceReferences.Select((reference, index) => new ResolveEntityReferenceLookup(
                index.ToString(CultureInfo.InvariantCulture),
                reference.EntityType,
                $"{reference.DataSource}.{reference.SourceEntityType}".ToLowerInvariant(),
                reference.EntityId)).ToList();
            var lookupResults = await new ResolveEntityReferenceLookupsProcessor(mediator)
                .ProcessAsync(lookups, cancellationToken);
            var resolvedReferences = new List<ResolvedEntityReference>();
            var failures = new List<ResolveEntityReferenceException>();

            for (var index = 0; index < sourceReferences.Count; index++)
            {
                var sourceReference = sourceReferences[index];
                var matches = lookupResults[index].Matches;
                if (matches.Count == 0)
                {
                    continue;
                }

                var firstMatch = matches[0];
                resolvedReferences.Add(new ResolvedEntityReference
                {
                    SourceEntityReference = sourceReference,
                    DataHubEntityReference = new ExternalEntityReference
                    {
                        EntityType = firstMatch.EntityType,
                        EntityId = firstMatch.EntityId,
                        _tag = null
                    }
                });

                for (var duplicateIndex = 1; duplicateIndex < matches.Count; duplicateIndex++)
                {
                    failures.Add(new ResolveEntityReferenceException(sourceReference));
                }
            }

            return new ResolveEntityReferencesResponse
            {
                Success = true,
                Results = resolvedReferences,
                ResolutionFailures = failures
            };
        }
        catch (Exception ex)
        {
            return new ResolveEntityReferencesResponse
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
