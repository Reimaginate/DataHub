using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Helpers;

internal sealed record SourceTrackingReassembly(
    JObject Entity,
    DateTimeOffset? RebasedTo,
    DateTimeOffset? FailedAt,
    string FailureReason);

internal static class SourceTrackingRebase
{
    public static SourceTrackingReassembly ReassemblePrefix(List<ChangeTrackingEntry> entries)
    {
        var timestamps = entries.Select(entry => entry.Timestamp).Distinct().OrderBy(timestamp => timestamp).ToList();
        try
        {
            return new SourceTrackingReassembly(ChangeTrackingHelper.ReassembleEntity(entries), timestamps.Last(), null, null);
        }
        catch (Exception exception)
        {
            JObject lastSuccessfulEntity = null;
            DateTimeOffset? lastSuccessfulTimestamp = null;
            var first = 0;
            var last = timestamps.Count - 2; // The complete history has already failed.

            // Strict replay stops at its first failure. Find the last complete, valid
            // timestamp group without replaying every possible prefix on healthy histories.
            while (first <= last)
            {
                var middle = first + (last - first) / 2;
                var prefix = entries.Where(entry => entry.Timestamp <= timestamps[middle]).ToList();
                try
                {
                    lastSuccessfulEntity = ChangeTrackingHelper.ReassembleEntity(prefix);
                    lastSuccessfulTimestamp = timestamps[middle];
                    first = middle + 1;
                }
                catch (Exception)
                {
                    last = middle - 1;
                }
            }

            // Each replay starts from a clone, so a partially applied failed patch
            // cannot leak into the successful checkpoint.
            return new SourceTrackingReassembly(lastSuccessfulEntity, lastSuccessfulTimestamp, timestamps[first], exception.Message);
        }
    }
}
