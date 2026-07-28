using JsonDiffPatchDotNet;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.TestFramework.Helpers;

internal static class ChangeTrackingHelper
{
    public static JObject? StripBaseProperties(JObject? entityData)
    {
        if (entityData == null) return null;

        var entityDataClone = (JObject)entityData.DeepClone();
        var propsToRemove = typeof(DataHubEntityBase).GetProperties();
        foreach (var pi in propsToRemove.Where(w => w.Name != nameof(DataHubEntity.alternateKeys)))
        {
            var prop = entityDataClone.Property(pi.Name, StringComparison.InvariantCultureIgnoreCase);
            prop?.Remove();
        }

        foreach (var pname in "_etag,_ts,_dnf,_rid,_self,_attachments".Split(","))
        {
            var prop = entityDataClone.Property(pname, StringComparison.InvariantCultureIgnoreCase);
            prop?.Remove();
        }

        return entityDataClone;
    }

    public static JObject ReassembleEntity(List<ChangeTrackingEntry> changeTrackingEntries)
    {
        try
        {
            var jdp = new JsonDiffPatch(new Options() { TextDiff = TextDiffMode.Simple });

            var init = changeTrackingEntries.FirstOrDefault(f => f.EntryType == ChangeTrackingEntryTypes.Init);
            if (init == null) throw new Exception("Could not find initial entry");

            var entityJson = init.Data;


            var modifiedOn = init.Timestamp;

            var updates = changeTrackingEntries.Where(w => w.EntryType == ChangeTrackingEntryTypes.Update && w.Timestamp >= init.Timestamp && w.Data != null).OrderBy(o => o.Timestamp).ThenBy(o => o._ts).ToList();
            if (updates.Any())
            {
                foreach (var trackingEntry in updates)
                {
                    entityJson = (JObject)jdp.Patch(entityJson, trackingEntry.Data);
                }

                modifiedOn = updates.Last().Timestamp;
            }

            entityJson[nameof(DataHubEntity.entityType)] = init.EntityType;
            entityJson[nameof(DataHubEntity.id)] = init.EntityId;
            entityJson[nameof(DataHubEntity.createdOn)] ??= init.Timestamp;
            entityJson[nameof(DataHubEntity.lastUpdated)] = modifiedOn;
            return entityJson;
        }
        catch (Exception ex)
        {
            throw new Exception("Could not reassemble entity from change tracking", ex);
        }
    }
}