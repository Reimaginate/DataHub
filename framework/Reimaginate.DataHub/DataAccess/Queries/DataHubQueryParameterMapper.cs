using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub;
using Reimaginate.DataServices;

namespace Reimaginate.DataHub.DataAccess.Queries;

internal static class DataHubQueryParameterMapper
{
    public static IReadOnlyCollection<QueryParameter> ToDataServiceParameters(IEnumerable<DataHubQueryParameter> parameters)
    {
        if (parameters == null)
        {
            return [];
        }

        return parameters
            .Select(parameter => new QueryParameter(parameter.Name, UnwrapValue(parameter.Value)))
            .ToList();
    }

    public static bool HasParameters(IEnumerable<QueryParameter> parameters)
    {
        return parameters?.Any() == true;
    }

    public static List<QueryParameter> Combine(params IEnumerable<QueryParameter>[] parameterSets)
    {
        return parameterSets
            .Where(parameterSet => parameterSet != null)
            .SelectMany(parameterSet => parameterSet)
            .ToList();
    }

    public static List<string> AddIndexedParameters<TValue>(
        IEnumerable<TValue> values,
        string namePrefix,
        ICollection<QueryParameter> parameters)
    {
        return values
            .Select((value, index) =>
            {
                var name = $"{namePrefix}{index}";
                parameters.Add(new QueryParameter(name, value));
                return $"@{name}";
            })
            .ToList();
    }

    private static object UnwrapValue(object value)
    {
        return value switch
        {
            JValue jValue => jValue.Value,
            JToken jToken => jToken.ToObject<object>(),
            _ => value
        };
    }
}
