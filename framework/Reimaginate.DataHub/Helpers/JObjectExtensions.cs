using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Helpers;

public static class JObjectExtensions
{
    public static List<JObject> Replace<T>(this List<JObject> jo, List<JObject> replacements, Func<JObject, T> matchers)
    {
        var replacementMatcherResults = replacements.Select(matchers).ToList();
        jo.RemoveAll(w => replacementMatcherResults.Contains(matchers(w)));
        jo.AddRange(replacements);

        return jo;
    }

    public static JObject RemoveNullValues(this JObject obj)
    {
        return (JObject)JTokenExtensions.RemoveNullValues(obj);
    }

    public static List<JObject> ExternalEntityReferences(this JObject jo)
    {
        return jo
            .Descendants()
            .OfType<JObject>()
            .Where(w => w.ContainsKey("@Tag") && w.Value<string>("@Tag") == nameof(ExternalEntityReference))
            .ToList();
    }

    public static T ToObjectIgnoreErrors<T>(this JObject jObject)
    {
        var ser = new JsonSerializer();
        ser.Error += (_, args) =>
        {
            if (args.ErrorContext.Error.Message.StartsWith("Error reading"))
            {
                args.ErrorContext.Handled = true;
            }
        };

        return jObject.ToObject<T>(ser);
    }

    public static JObject SortProperties(this JObject obj)
    {
        var sortedObj = new JObject();

        foreach (var property in obj.Properties().OrderBy(p => p.Name))
        {
            switch (property.Value)
            {
                case JObject nestedObj:
                    sortedObj.Add(property.Name, nestedObj.SortProperties());
                    break;

                case JArray array:
                    sortedObj.Add(property.Name, SortJArray(array));
                    break;

                default:
                    sortedObj.Add(property.Name, property.Value);
                    break;
            }
        }

        return sortedObj;
    }

    private static JArray SortJArray(JArray array)
    {
        var sortedArray = new JArray();

        foreach (var item in array)
        {
            if (item is JObject nestedObj)
            {
                sortedArray.Add(nestedObj.SortProperties());
            }
            else
            {
                sortedArray.Add(item);
            }
        }

        return sortedArray;
    }

    public static Dictionary<string, JToken> GetAllProperties(this JObject jObject)
    {
        var properties = new Dictionary<string, JToken>();
        RetrieveProperties(jObject, properties, string.Empty);
        return properties;
    }

    private static void RetrieveProperties(JObject jObject, Dictionary<string, JToken> properties, string parentPath)
    {
        foreach (var property in jObject.Properties())
        {
            var currentPath = string.IsNullOrEmpty(parentPath) ? property.Name : $"{parentPath}.{property.Name}";
            if (property.Value is JObject nestedObject)
            {
                RetrieveProperties(nestedObject, properties, currentPath);
            }
            else
            {
                properties[currentPath] = property.Value;
            }
        }
    }

    public static void SetProperty(this JObject jObject, string path, JToken value)

    {

        var parts = path.Split('.');
        var currentObject = jObject;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (currentObject[part] == null)
            {
                currentObject[part] = new JObject();
            }

            currentObject = (JObject)currentObject[part];
        }

        currentObject[parts[^1]] = value;
    }

    public static void RemoveProperty(this JObject jObject, string path)
    {
        var parts = path.Split('.');
        var currentObject = jObject;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (currentObject[part] is JObject nestedObject)
            {
                currentObject = nestedObject;
            }
            else if (currentObject[part] is JArray array && int.TryParse(parts[i + 1].Trim('[', ']'), out var index))
            {
                if (index >= 0 && index < array.Count)
                {
                    array.RemoveAt(index);
                    return;
                }
            }
            else
            {
                // If the path does not exist, exit the function
                return;
            }
        }

        currentObject.Remove(parts[^1]);
    }
}

