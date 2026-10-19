using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Helpers;

internal static class RebaseTrackingBaselines
{
    public static DateTimeOffset? FirstConflict(IEnumerable<ChangeTrackingEntry> entries) => entries
        .Where(entry => entry.EntryType == ChangeTrackingEntryTypes.Init)
        .GroupBy(entry => new { entry.DataSource, entry.EntityType, entry.EntityId, entry.Timestamp })
        .Where(group => group.Skip(1).Any(entry => !JToken.DeepEquals(group.First().Data, entry.Data)))
        .Select(group => (DateTimeOffset?)group.Key.Timestamp).OrderBy(timestamp => timestamp).FirstOrDefault();

    public static ChangeTrackingEntry CreationDateBaseline(IEnumerable<ChangeTrackingEntry> entries, ChangeTrackingEntry baseline)
    {
        if (baseline == null) return null;
        var candidates = entries.Where(entry => entry.EntryType == ChangeTrackingEntryTypes.Init &&
            entry.DataSource == baseline.DataSource && entry.EntityType == baseline.EntityType &&
            entry.EntityId == baseline.EntityId && entry.Timestamp == baseline.Timestamp).ToList();
        var expected = CreationDate(baseline);
        // Recovery may use known tracking creation metadata, but must not choose
        // arbitrarily when the conflicting snapshots disagree about that date.
        return candidates.All(entry => JToken.DeepEquals(CreationDate(entry), expected)) ? baseline : null;
    }

    private static JToken CreationDate(ChangeTrackingEntry entry) =>
        entry.Data?[nameof(DataHubEntity.createdOn)] is { Type: not JTokenType.Null } value ? value : new JValue(entry.Timestamp);
}
