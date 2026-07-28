using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using System;
using System.Globalization;

namespace Reimaginate.DataHub.Helpers;

public static class JTokenExtensions
{
    public static string DataHubEntityId(this JToken jt)
    {
        return jt.Value<string>(nameof(DataHubEntity.id));
    }

    public static string DataHubEntityType(this JToken jt)
    {
        return jt.Value<string>(nameof(DataHubEntity.entityType));
    }

    public static DateTimeOffset DataHubLastUpdated(this JToken jo)
    {
        return jo.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated));
    }

    public static DateTimeOffset? DateTimeOffsetValue(this JToken jt, string key)
    {
        return jt[key].AsDateTimeOffset();
    }

    public static DateTimeOffset DateTimeOffsetValueRequired(this JToken jt, string key)
    {
        var value = jt.DateTimeOffsetValue(key);
        if (value == null)
        {
            throw new InvalidOperationException($"Token '{key}' could not be converted to DateTimeOffset.");
        }

        return value.Value;
    }

    public static DateTimeOffset? AsDateTimeOffset(this JToken jt)
    {
        if (jt == null || jt.Type == JTokenType.Null)
        {
            return null;
        }

        if (jt.Type == JTokenType.String)
        {
            var value = jt.Value<string>();
            return string.IsNullOrWhiteSpace(value)
                ? null
                : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        return jt.Value<DateTimeOffset?>();
    }

    public static bool TryAsDateTimeOffset(this JToken jt, out DateTimeOffset value)
    {
        value = default;

        if (jt == null || jt.Type == JTokenType.Null)
        {
            return false;
        }

        if (jt.Type == JTokenType.String)
        {
            var stringValue = jt.Value<string>();
            return !string.IsNullOrWhiteSpace(stringValue)
                   && DateTimeOffset.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);
        }

        if (jt is JValue { Value: DateTimeOffset dateTimeOffsetValue })
        {
            value = dateTimeOffsetValue;
            return true;
        }

        if (jt is JValue { Value: DateTime dateTimeValue })
        {
            value = dateTimeValue.Kind == DateTimeKind.Utc
                ? new DateTimeOffset(dateTimeValue, TimeSpan.Zero)
                : new DateTimeOffset(dateTimeValue);
            return true;
        }

        try
        {
            var dateTimeOffset = jt.Value<DateTimeOffset?>();
            if (dateTimeOffset == null)
            {
                return false;
            }

            value = dateTimeOffset.Value;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    public static JToken RemoveNullValues(this JToken jt)
    {
        switch (jt.Type)
        {
            case JTokenType.Object:
            {
                var copy = new JObject();
                foreach (var prop in jt.Children<JProperty>())
                {
                    var child = prop.Value;
                    if (child.HasValues)
                    {
                        child = RemoveNullValues(child);
                    }
                    if (child.Type != JTokenType.Null)
                    {
                        copy.Add(prop.Name, child);
                    }
                }
                return copy;
            }
            case JTokenType.Array:
            {
                var copy = new JArray();
                foreach (var item in jt.Children())
                {
                    var child = item;
                    if (item.HasValues)
                    {
                        child = RemoveNullValues(child);
                    }
                    if (child.Type != JTokenType.Null)
                    {
                        copy.Add(child);
                    }
                }
                return copy;
            }
            default:
                return jt;
        }
    }
    
    public static bool IsEmpty(this JToken token)
    {
        if (token == null || token.Type == JTokenType.None || token.Type == JTokenType.Null)
        {
            return true;
        }

        if (token.Type == JTokenType.Array && !token.HasValues)
        {
            return true;
        }

        if (token.Type == JTokenType.Object && !token.HasValues)
        {
            return true;
        }

        return false;
    }

}
