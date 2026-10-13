using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Helpers;

internal static class MergeBatchIntegrity
{
    // Multiple versions of one source are supported. Different source identities in
    // the same processing group must not compete for one destination/result owner.
    public static List<MergeEntityRequest> ExcludeConflictingSources(List<MergeEntityRequest> inputs,
        List<ResolvedEntityReference> references, List<MergeEntityResult> results)
    {
        var destinations = references.ToLookup(r => (r.SourceEntityReference.DataSource,
            r.SourceEntityReference.SourceEntityType, r.SourceEntityReference.EntityId));
        var participants = inputs.Select(i => (Input: i, Destination: destinations[
            (i.DataSource, i.SourceEntityType, i.SourceEntityId)].First().DataHubEntityReference.EntityId)).ToList();
        var conflicts = participants.GroupBy(p => p.Destination)
            .Where(g => g.Select(p => (p.Input.DataSource, p.Input.SourceEntityType, p.Input.SourceEntityId)).Distinct().Count() > 1)
            .Select(g => g.Key).ToHashSet();
        foreach (var participant in participants.Where(p => conflicts.Contains(p.Destination))
                     .DistinctBy(p => (p.Input.DataSource, p.Input.SourceEntityType, p.Input.SourceEntityId)))
        {
            var input = participant.Input;
            results.Add(new MergeEntityResult
            {
                DataSource = input.DataSource, SourceEntityType = input.SourceEntityType,
                SourceEntityId = input.SourceEntityId, DataHubEntityType = input.DataHubEntityType,
                DataHubEntityId = participant.Destination, MergeOutcome = MergeOutcomes.MergeFailed,
                FailureReason = "Different source entities in this merge group resolve to the same destination. Submit them in separate merge calls."
            });
        }
        return participants.Where(p => !conflicts.Contains(p.Destination)).Select(p => p.Input).ToList();
    }

    public static void ValidateAlternateKeys(JObject input)
    {
        var keys = input[nameof(DataHubEntity.alternateKeys)];
        if (keys == null || keys.Type == JTokenType.Null) return;
        if (keys is not JArray array || array.Any(k => k is not JObject || k[nameof(AlternateKey.Key)]?.Type != JTokenType.String))
            throw new ArgumentException("alternateKeys must be an array of objects with a string Key.");
    }

    // A delta was calculated against an observed source state that may have been
    // rejected at the destination. Rebase only changed fields. Object additions or
    // replacements follow the incoming partial-object merge semantics; arrays still
    // replace in full, and explicit deletions still delete.
    public static JObject ApplyChange(JObject accepted, JObject delta)
    {
        return (JObject)ChangeTrackingHelper.JsonDiffPatch.Patch(accepted.DeepClone(), PrepareChange(accepted, delta));
    }

    public static JObject MergeInput(JObject accepted, JObject input)
    {
        var merged = (JObject)accepted.DeepClone();
        merged.Merge(input, new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            MergeNullValueHandling = MergeNullValueHandling.Merge
        });
        return merged.RemoveNullValues();
    }

    public static void AccumulateChangedFields(JObject mask, JObject delta)
    {
        foreach (var property in delta.Properties())
        {
            if (mask[property.Name]?.Type == JTokenType.Boolean) continue;
            if (property.Value is JObject nested && nested["_t"]?.Type != JTokenType.String)
            {
                var child = mask[property.Name] as JObject ?? new JObject();
                AccumulateChangedFields(child, nested);
                mask[property.Name] = child;
            }
            else mask[property.Name] = true;
        }
    }

    public static JObject SelectSuppliedChanges(JObject input, JObject currentDelta, JObject rejectedFields)
    {
        var mask = (JObject)rejectedFields.DeepClone();
        AccumulateChangedFields(mask, currentDelta);
        return SelectFields(input, mask);
    }

    private static JObject SelectFields(JObject input, JObject mask)
    {
        var selected = new JObject();
        foreach (var property in input.Properties())
        {
            if (mask[property.Name] is JObject nested && property.Value is JObject incoming)
            {
                var child = SelectFields(incoming, nested);
                if (child.HasValues) selected[property.Name] = child;
            }
            else if (mask[property.Name]?.Type == JTokenType.Boolean)
                selected[property.Name] = property.Value.DeepClone();
        }
        return selected;
    }

    public static JObject PrepareChange(JObject accepted, JObject delta)
    {
        // Ordinary deltas need no extra entity clone or diff. Clone the delta lazily
        // only when an object shape differs from its observed source baseline.
        JObject patch = null;
        foreach (var property in delta.Properties())
        {
            JToken replacement = property.Value;
            if (property.Value is JObject nested && nested.Value<string>("_t") != "a")
            {
                var current = accepted[property.Name] as JObject;
                if (current != null) replacement = PrepareChange(current, nested);
                else
                {
                    var empty = new JObject();
                    replacement = new JArray(ChangeTrackingHelper.JsonDiffPatch.Patch(empty, PrepareChange(empty, nested)));
                }
            }
            else if (property.Value is JArray values && values.Count is 1 or 2 &&
                     values.Last is JObject incoming && accepted[property.Name] is JObject existing)
            {
                var merged = (JObject)existing.DeepClone();
                merged.Merge(incoming, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Replace,
                    MergeNullValueHandling = MergeNullValueHandling.Merge
                });
                var rebased = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(existing, merged.RemoveNullValues());
                replacement = rebased;
            }
            if (ReferenceEquals(replacement, property.Value)) continue;
            patch ??= (JObject)delta.DeepClone();
            if (replacement == null) patch.Remove(property.Name);
            else patch[property.Name] = replacement;
        }
        return patch ?? delta;
    }
}
