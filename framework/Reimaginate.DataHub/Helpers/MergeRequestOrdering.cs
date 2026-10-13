using System;
using System.Collections.Generic;
using System.Linq;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Helpers;

internal static class MergeRequestOrdering
{
    public static List<MergeEntityRequest> SortSourceVersions(IEnumerable<MergeEntityRequest> requests, List<MergeEntityResult> results)
    {
        var ordered = requests.ToArray();
        var sourceGroups = ordered.Select((request, index) => (Request: request, Index: index))
            .GroupBy(item => (item.Request.DataSource, item.Request.SourceEntityType,
                item.Request.SourceEntityId, item.Request.DataHubEntityType));

        foreach (var group in sourceGroups)
        {
            // A single input needs no ordering and retains its existing timestamp handling.
            if (group.Count() < 2) continue;

            var versions = new List<(MergeEntityRequest Request, DateTimeOffset Timestamp)>();
            foreach (var item in group)
            {
                try
                {
                    var timestamp = item.Request.Data?.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated))
                        ?? throw new InvalidOperationException("Incoming entity data is missing.");
                    versions.Add((item.Request, timestamp));
                }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException or InvalidCastException)
                {
                    results.Add(new MergeEntityResult
                    {
                        DataSource = item.Request.DataSource,
                        SourceEntityType = item.Request.SourceEntityType,
                        SourceEntityId = item.Request.SourceEntityId,
                        DataHubEntityType = item.Request.DataHubEntityType,
                        MergeOutcome = MergeOutcomes.MergeFailed,
                        FailureReason = "Cannot order repeated source versions: incoming lastUpdated must be a valid timestamp."
                    });
                }
            }

            // OrderBy is stable: equal timestamps retain arrival order. Replace only this
            // source's slots, preserving the positions of independent source identities.
            var sorted = versions.OrderBy(version => version.Timestamp).Select(version => version.Request).ToArray();
            var ordinal = 0;
            foreach (var item in group)
                ordered[item.Index] = ordinal < sorted.Length ? sorted[ordinal++] : null;
        }

        return ordered.Where(request => request != null).ToList();
    }
}
