using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.Services.DynamicAssemblies;
using System.Text.RegularExpressions;
using Reimaginate.DataHub.SharedModels.Core.Interfaces;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.FindPotentialDuplicates;

public class FindPotentialDuplicatesRequestHandler(IMediator mediator, IDynamicAssembliesService dynamicAssembliesService)
    : IHandler<FindPotentialDuplicatesRequest, FindPotentialDuplicatesResponse>
{
    public async Task<FindPotentialDuplicatesResponse> HandleAsync(FindPotentialDuplicatesRequest request, CancellationToken cancellationToken)
    {
        var results = new JArray();

        var forEntities = new List<JObject>(request.EntitiesToMatch);
        var dataHubEntityType = request.DataHubEntityType;

        var findVal = Detokenize(request.DuplicatePreventionRule.Find);

        var duplicateResolver = dynamicAssembliesService.LoadAssemblyAndReturnType<IDuplicateResolver>(DynamicAssemblyTypes.DuplicateResolver,
            $"DuplicateResolver.{dataHubEntityType}",
            [findVal, request.DuplicatePreventionRule.Match],
            "DuplicateResolver");

        while (forEntities.Count > 0)
        {
            var batch = forEntities.Take(100).ToList();

            var matches = await duplicateResolver.FindPotentialDuplicatesAsync(mediator, dataHubEntityType, batch, cancellationToken);

            foreach (var item in matches)
            {
                results.Add(item);
            }

            forEntities.RemoveRange(0, batch.Count);
        }

        var distinctResults = results.DistinctBy(d => d.Value<string>(nameof(DataHubEntity.id))).ToList();

        duplicateResolver = null;

        return new FindPotentialDuplicatesResponse()
        {
            PotentialDuplicates = new JArray(distinctResults)
        };
    }

    public string Detokenize(string input)
    {
        // Replace @In and @PathIn tokens

        var output = NormalizeLegacyStringLiterals(input);

        output = Regex.Replace(output, @"@In<(\w+)>\(\$(\w+)\)", m =>
        {
            var field = FieldExpression(m.Groups[1].Value, $"x.{m.Groups[2].Value}");
            return $"{field} in ({{In<{m.Groups[1].Value}>(i, \"{m.Groups[2].Value}\")}})";
        });
        output = Regex.Replace(output, @"@PathIn<(\w+)>\(\$(\w+)\.(\w+)\)", m =>
        {
            var field = FieldExpression(m.Groups[1].Value, $"x.{m.Groups[2].Value}.{m.Groups[3].Value}");
            return $"{field} in ({{PathIn<{m.Groups[1].Value}>(i, \"{m.Groups[2].Value}.{m.Groups[3].Value}\")}})";
        });
        output = Regex.Replace(output, @"@In<(\w+)>\(\$(\w+)\, \$(\w+)\)", m =>
        {
            var field = FieldExpression(m.Groups[1].Value, $"x.{m.Groups[2].Value}");
            return $"{field} in ({{In<{m.Groups[1].Value}>(i, \"{m.Groups[3].Value}\")}})";
        });

        output = Regex.Replace(output, @"\$(\w+)", m => $"'{EscapeCosmosStringLiteral(m.Groups[1].Value)}'");

        return output;
    }

    private static string NormalizeLegacyStringLiterals(string input)
    {
        return Regex.Replace(input, "\\\\\"([^\\\\\"]*)\\\\\"", m => $"'{EscapeCosmosStringLiteral(m.Groups[1].Value)}'");
    }

    private static string EscapeCosmosStringLiteral(string value)
    {
        return value.Replace("'", "\\'");
    }

    private static string FieldExpression(string typeName, string fieldPath)
    {
        return string.Equals(typeName, "string", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(typeName, "char", System.StringComparison.OrdinalIgnoreCase)
            ? $"LOWER({fieldPath})"
            : fieldPath;
    }
}
