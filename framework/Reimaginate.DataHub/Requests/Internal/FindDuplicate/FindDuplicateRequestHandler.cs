using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Services.DynamicAssemblies;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Interfaces;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.FindDuplicate;

public class FindDuplicateRequestHandler(IDynamicAssembliesService dynamicAssembliesService) : IHandler<FindDuplicateRequest, FindDuplicateResponse>
{
    public Task<FindDuplicateResponse> HandleAsync(FindDuplicateRequest request, CancellationToken cancellationToken)
    {
        var duplicateResolver = dynamicAssembliesService.LoadAssemblyAndReturnType<IDuplicateResolver>(DynamicAssemblyTypes.DuplicateResolver,
            $"DuplicateResolver.{request.DataHubEntityType}",
            [request.DuplicatePreventionRule.Find, request.DuplicatePreventionRule.Match],
            "DuplicateResolver");

        var potentialDuplicates = request.PotentialDuplicates;
        var entityToMatch = request.EntityToMatch;
        var dataSource = request.DataSource;
        var sourceEntityType = request.SourceEntityType;

        JObject duplicate = null;

        var matches = duplicateResolver.Resolve(entityToMatch, potentialDuplicates).ToList();

        var bestMatch = matches.OrderBy(o =>
        {
            var createdOn = o.DateTimeOffsetValueRequired(nameof(DataHubEntity.createdOn));
            return createdOn;
        }).ToList().LastOrDefault();

        if (bestMatch != null)
        {
            var matchedEntity = (JObject)bestMatch;
            var matchedEntityAltKeys = (JArray)matchedEntity[nameof(DataHubEntity.alternateKeys)]!;

            var altKeyKey = $"{dataSource}.{sourceEntityType}".ToLower();
            var existingKey = matchedEntityAltKeys?.FirstOrDefault(a => a.Value<string>(nameof(AlternateKey.Key)) == altKeyKey);
            if (existingKey == null) duplicate = matchedEntity;
        }

        return Task.FromResult(new FindDuplicateResponse()
        {
            DuplicateEntity = duplicate
        });
    }
}
