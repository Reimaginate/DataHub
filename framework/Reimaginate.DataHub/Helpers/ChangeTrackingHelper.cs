using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using JsonDiffPatchDotNet;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Helpers;

internal static class ChangeTrackingHelper
{
    private static readonly PropertyInfo[] BaseProperties = typeof(DataHubEntityBase).GetProperties();
    private static readonly string[] AdditionalPropertiesToRemove = { "_etag", "_ts", "_dnf" };
    public static JsonDiffPatch JsonDiffPatch { get; set; } = new JsonDiffPatch(new Options()
    {
        TextDiff = TextDiffMode.Simple,
        ArrayDiff = ArrayDiffMode.Simple
    });

    public static JObject StripBaseProperties(JObject entityData)
    {
        if (entityData == null) return null;

        var entityDataClone = (JObject)entityData.DeepClone();
        foreach (var pi in BaseProperties)
        {
            if (pi.Name == nameof(DataHubEntity.alternateKeys)) continue;

            var prop = entityDataClone.Property(pi.Name, StringComparison.InvariantCultureIgnoreCase);
            prop?.Remove();
        }

        foreach (var pname in AdditionalPropertiesToRemove)
        {
            var prop = entityDataClone.Property(pname, StringComparison.InvariantCultureIgnoreCase);
            prop?.Remove();
        }

        return entityDataClone;
    }

    public static JToken DiffIgnoringEquivalentMixedDateTokens(JToken left, JToken right)
    {
        return DiffIgnoringEquivalentValueTokens(left, right);
    }

    public static JToken DiffIgnoringEquivalentValueTokens(JToken left, JToken right)
    {
        var normalizedLeft = left?.DeepClone();
        var normalizedRight = right?.DeepClone();

        (normalizedLeft, normalizedRight) = NormalizeEquivalentValueTokens(normalizedLeft, normalizedRight);

        return JsonDiffPatch.Diff(normalizedLeft, normalizedRight);
    }

    public static JObject ReassembleEntity(List<ChangeTrackingEntry> changeTrackingEntries, bool skipPatchFailures = false)
    {
        if (changeTrackingEntries == null || changeTrackingEntries.Count == 0)
            throw new ArgumentException("changeTrackingEntries must not be null or empty.");

        try
        {
            var init = changeTrackingEntries.FirstOrDefault(f => f.EntryType == ChangeTrackingEntryTypes.Init);
            if (init == null) throw new Exception("Could not find initial entry");

            var entityJson = (JObject)init.Data.DeepClone();
            var modifiedOn = init.Timestamp;

            var updatesSinceInit = changeTrackingEntries.Where(w => w.EntryType == ChangeTrackingEntryTypes.Update && w.Timestamp >= init.Timestamp && w.Data != null).ToList();

            var timeStampGroups = updatesSinceInit.OrderBy(o => o.Timestamp).GroupBy(g => g.Timestamp);
            var orderedUpdates = timeStampGroups.SelectMany(grp =>
            {
                var ret = grp.Where(w => w._ts != 0).OrderBy(o => o.Timestamp).ThenBy(o => o._ts).ToList();
                ret.AddRange(grp.Where(w => w._ts == 0).OrderBy(o => o.Timestamp));
                return ret;

            }).ToList();

            if (orderedUpdates.Any())
            {
                var patches = orderedUpdates.Select(update => (JObject)update.Data.DeepClone()).ToList();

                foreach (var patch in patches.Where(w => w.HasValues))
                {
                    var arrayPatches = patch.Descendants().OfType<JObject>().Where(w => w.ContainsKey("_t") && w["_t"]!.Value<string>() == "a").ToList();
                    arrayPatches.ForEach(arrPatch =>
                    {
                        var dst = entityJson.SelectToken(arrPatch.Path);
                        if (dst == null || dst.Type == JTokenType.Null)
                        {
                            entityJson[arrPatch.Path] = new JArray();
                        }
                    });

                    try
                    {
                        entityJson = (JObject)JsonDiffPatch.Patch(entityJson, patch);
                    }
                    catch (Exception)
                    {
                        if (!skipPatchFailures) throw;
                    }
                }
                
                modifiedOn = orderedUpdates.Last().Timestamp;
            }

            entityJson[nameof(DataHubEntity.entityType)] = init.EntityType;
            entityJson[nameof(DataHubEntity.id)] = init.EntityId;
            if (entityJson[nameof(DataHubEntity.createdOn)] == null || entityJson[nameof(DataHubEntity.createdOn)]!.Type == JTokenType.Null)
            {
                entityJson[nameof(DataHubEntity.createdOn)] =  init.Timestamp;
            }

            entityJson[nameof(DataHubEntity.lastUpdated)] = modifiedOn;
            return entityJson;
        }
        catch (Exception ex)
        {
            var message = $"Could not reassemble entity from change tracking: {ex.Message}";
            throw new Exception(message);
        }
    }

    private static (JToken Left, JToken Right) NormalizeEquivalentValueTokens(JToken left, JToken right)
    {
        if (left == null || right == null)
        {
            return (left, right);
        }

        if (IsEquivalentMixedDateTokenPair(left, right, out var canonicalValue))
        {
            var canonicalToken = new JValue(canonicalValue);
            return (canonicalToken, (JToken)canonicalToken.DeepClone());
        }

        if (IsEquivalentNumericTokenPair(left, right))
        {
            var canonicalToken = left.DeepClone();
            return (canonicalToken, (JToken)canonicalToken.DeepClone());
        }

        if (left is JObject leftObject && right is JObject rightObject)
        {
            foreach (var leftProperty in leftObject.Properties().ToList())
            {
                var rightProperty = rightObject.Property(leftProperty.Name);
                if (rightProperty == null)
                {
                    continue;
                }

                (leftProperty.Value, rightProperty.Value) = NormalizeEquivalentValueTokens(leftProperty.Value, rightProperty.Value);
            }
        }
        else if (left is JArray leftArray && right is JArray rightArray)
        {
            var count = Math.Min(leftArray.Count, rightArray.Count);
            for (var i = 0; i < count; i++)
            {
                (leftArray[i], rightArray[i]) = NormalizeEquivalentValueTokens(leftArray[i], rightArray[i]);
            }
        }

        return (left, right);
    }

    private static bool IsEquivalentMixedDateTokenPair(JToken left, JToken right, out string canonicalValue)
    {
        canonicalValue = null;

        var leftIsDateToken = left.Type == JTokenType.Date;
        var rightIsDateToken = right.Type == JTokenType.Date;
        var leftIsStringToken = left.Type == JTokenType.String;
        var rightIsStringToken = right.Type == JTokenType.String;

        if (!((leftIsDateToken && rightIsStringToken) || (leftIsStringToken && rightIsDateToken)))
        {
            return false;
        }

        if (!left.TryAsDateTimeOffset(out var leftDate) || !right.TryAsDateTimeOffset(out var rightDate))
        {
            return false;
        }

        if (leftDate.ToUniversalTime() != rightDate.ToUniversalTime())
        {
            return false;
        }

        canonicalValue = leftDate.ToUniversalTime().ToString("O");
        return true;
    }

    private static bool IsEquivalentNumericTokenPair(JToken left, JToken right)
    {
        if (!IsNumericToken(left) || !IsNumericToken(right))
        {
            return false;
        }

        if (!TryGetDecimalValue(left, out var leftValue) || !TryGetDecimalValue(right, out var rightValue))
        {
            return false;
        }

        return leftValue == rightValue;
    }

    private static bool IsNumericToken(JToken token)
    {
        return token.Type is JTokenType.Integer or JTokenType.Float;
    }

    private static bool TryGetDecimalValue(JToken token, out decimal value)
    {
        value = default;

        if (token is not JValue { Value: { } rawValue })
        {
            return false;
        }

        try
        {
            switch (rawValue)
            {
                case double doubleValue when double.IsFinite(doubleValue):
                    value = Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
                    return true;

                case float floatValue when float.IsFinite(floatValue):
                    value = Convert.ToDecimal(floatValue, CultureInfo.InvariantCulture);
                    return true;

                case double:
                case float:
                    return false;

                default:
                    value = Convert.ToDecimal(rawValue, CultureInfo.InvariantCulture);
                    return true;
            }
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }
}
