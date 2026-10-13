using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataHub.Test.Unit.Tests;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Benchmarks;

// Manual local measurements, without external services or timing pass/fail thresholds.
// dotnet build test/Reimaginate.DataHub.Test.Unit -c Release -p:RunMergePerformanceBenchmarks=true
// dotnet test/Reimaginate.DataHub.Test.Unit/bin/Release/net10.0/Reimaginate.DataHub.Test.Unit.dll
//   --filter-class '*MergePerformanceMeasurements' --parallel none --output Detailed
// End-to-end samples use indexed mocked reads and immediate mock writes, and still
// include mock cloning/notification overhead. They are not production throughput or RU estimates.
public class MergePerformanceMeasurements(ITestOutputHelper output)
{
    private const int Repetitions = 3;
    private const int LinesPerBooking = 10;

    [Fact]
    [Trait("Category", "MergePerformance")]
    public async Task Measure_current_batches_and_prototype_source_history_rebasing()
    {
        var results = new List<Measurement>();
        await MeasureBatch(true, 20, 2); // JIT and handler warm-up, excluded from results.
        await MeasureBatch(false, 20, 2);

        foreach (var tracked in new[] { false, true })
        {
            foreach (var count in new[] { 100, 500, 1000, 5000 })
            {
                var measurement = await Repeat($"current-{(tracked ? "tracked" : "untracked")}-two-updates-per-entity", count,
                    () => MeasureBatch(tracked, count, 2));
                results.Add(measurement);
                if (measurement.MedianMilliseconds > 10000) break;
            }

            foreach (var count in new[] { 25, 100, 250, 500, 1000 })
            {
                var measurement = await Repeat($"current-{(tracked ? "tracked" : "untracked")}-one-repeated-entity", count,
                    () => MeasureBatch(tracked, count, count));
                results.Add(measurement);
                if (measurement.MedianMilliseconds > 10000) break;
            }
        }

        // Isolate the extra source-history algorithm. Baseline prepares current
        // already-calculated source deltas; prototype applies and re-diffs them
        // against accepted working state. The production fix now rebases selectively;
        // retain this eager prototype as a reference for its avoidable allocation cost.
        foreach (var count in new[] { 100, 1000, 5000 })
        {
            var requests = Requests(count, count);
            var initial = MergeRegressionHarness.Booked(version: 0).Data;
            var deltas = SourceDeltas(initial, requests);
            MeasureSourceHistory(initial, deltas, false, 0); // Warm up both paths.
            MeasureSourceHistory(initial, deltas, true, 0);
            results.Add(await Repeat("source-history-prepare-existing-deltas", count,
                () => Task.FromResult(MeasureSourceHistory(initial, deltas, false, 0))));
            results.Add(await Repeat("prototype-source-history-rebase-no-rejections", count,
                () => Task.FromResult(MeasureSourceHistory(initial, deltas, true, 0))));
            results.Add(await Repeat("prototype-source-history-rebase-reject-every-fifth", count,
                () => Task.FromResult(MeasureSourceHistory(initial, deltas, true, 5))));
        }

        var report = new
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            LogicalProcessors = Environment.ProcessorCount,
            Configuration = "Release",
            LinesPerBooking,
            Repetitions,
            Notes = new[]
            {
                "Current batch timings exclude fixture/input setup. Mock reads use indexes, writes complete immediately; mock cloning/notification overhead remains. No database or network latency.",
                "Allocations are process-wide managed bytes during each measured interval; run this class alone with --parallel none.",
                "Source-history prototype timings exclude input-delta calculation and correctness replay, and measure only preparation/rebasing.",
                "The eager prototype is a historical algorithm estimate, not the selective production rejection fix; use SourceHistoryFixPerformanceMeasurements for that comparison.",
                "The prototype does not measure event correlation, persistence ordering, Cosmos RU, lock contention, or historical-data repair.",
                "Stop increasing an individual scenario when its median exceeds 10 seconds; each operation has a 60-second timeout."
            },
            Results = results
        };
        var directory = Path.GetFullPath("TestResults/performance");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "merge-performance.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        var csv = "Scenario,Changes,MedianMilliseconds,MedianAllocatedMiB,MedianGen0,MedianGen1,MedianGen2,ProjectionWrites,HistoryUpdates\n"
            + string.Join("\n", results.Select(r => string.Join(",", r.Scenario, r.Changes,
                r.MedianMilliseconds.ToString("F3", CultureInfo.InvariantCulture), r.MedianAllocatedMiB.ToString("F3", CultureInfo.InvariantCulture),
                r.MedianGen0, r.MedianGen1, r.MedianGen2, r.ProjectionWrites, r.HistoryUpdates)));
        await File.WriteAllTextAsync(Path.Combine(directory, "merge-performance.csv"), csv, TestContext.Current.CancellationToken);
        output.WriteLine($"Saved measurements: {path}");
    }

    private async Task<Measurement> Repeat(string scenario, int count, Func<Task<Sample>> run)
    {
        var samples = new List<Sample>();
        for (var i = 0; i < Repetitions; i++) samples.Add(await run());
        var result = new Measurement(scenario, count,
            Median(samples.Select(s => s.Milliseconds)), Median(samples.Select(s => (double)s.AllocatedBytes)) / 1048576,
            Median(samples.Select(s => (double)s.Gen0)), Median(samples.Select(s => (double)s.Gen1)), Median(samples.Select(s => (double)s.Gen2)),
            samples[0].ProjectionWrites, samples[0].HistoryUpdates, samples);
        output.WriteLine($"{scenario}, changes={count}, median={result.MedianMilliseconds:F2} ms, allocated={result.MedianAllocatedMiB:F2} MiB");
        return result;
    }

    private static async Task<Sample> MeasureBatch(bool tracked, int count, int updatesPerEntity)
    {
        var requests = Requests(count, updatesPerEntity);
        using var db = new MergeRegressionHarness(false) { OperationTimeout = TimeSpan.FromSeconds(60), PerformanceMode = true };
        var sourceIds = requests.Select(r => r.SourceEntityId).Distinct().ToArray();
        foreach (var id in sourceIds) db.SeedBooking(id, "entity-" + id);
        Collect();
        var start = Snapshot();
        var stopwatch = Stopwatch.StartNew();
        var response = await db.MergePublicAsync(tracked, requests);
        stopwatch.Stop();
        var end = Snapshot();

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        foreach (var final in requests.GroupBy(r => r.SourceEntityId).Select(g => g.Last()))
        {
            var stored = db.Stored["entity-" + final.SourceEntityId];
            stored.Value<decimal>("Total").Should().Be(final.Data.Value<decimal>("Total"));
            stored["BookingLines"]!.ToObject<JObject[]>()!.Should().HaveCount(LinesPerBooking);
        }
        var writes = sourceIds.Sum(id => db.EntityWriteCount("entity-" + id));
        writes.Should().Be(sourceIds.Length, "a batch should persist one projection per destination");
        var updates = tracked ? sourceIds.Sum(id =>
            db.HistoryFor("TestSource", "Reservation", id).Count(e => e.EntryType == ChangeTrackingEntryTypes.Update)
            + db.HistoryFor(DataSources.DataHub, "Booking", "entity-" + id).Count(e => e.EntryType == ChangeTrackingEntryTypes.Update)) : 0;
        if (tracked) updates.Should().Be(2 * count);
        return SampleFrom(stopwatch.Elapsed.TotalMilliseconds, start, end, writes, updates);
    }

    private static List<MergeEntityRequest> Requests(int count, int updatesPerEntity) => Enumerable.Range(0, count).Select(index =>
    {
        var version = index % updatesPerEntity + 1;
        var request = MergeRegressionHarness.Booked("booking-" + index / updatesPerEntity, version);
        request.Data["Total"] = version;
        request.Data["Customer"] = new JObject { ["Name"] = "Generated customer", ["Email"] = "example@example.invalid", ["Reference"] = "customer-1" };
        request.Data["BookingLines"] = new JArray(Enumerable.Range(0, LinesPerBooking).Select(line => new JObject
        {
            ["Ref"] = "line-" + line, ["Status"] = "Version-" + version,
            ["Updated"] = MergeRegressionHarness.SourceTime(version), ["Quantity"] = 1, ["Price"] = 10m
        }));
        return request;
    }).ToList();

    private static List<ChangeTrackingEntry> SourceDeltas(JObject initial, List<MergeEntityRequest> requests)
    {
        var state = (JObject)initial.DeepClone();
        var result = new List<ChangeTrackingEntry>();
        foreach (var request in requests)
        {
            var next = (JObject)state.DeepClone();
            next.Merge(request.Data, new Newtonsoft.Json.Linq.JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
            result.Add(new ChangeTrackingEntry
            {
                EntryType = ChangeTrackingEntryTypes.Update, DataSource = "TestSource", EntityType = "Reservation", EntityId = request.SourceEntityId,
                Timestamp = request.Data.Value<DateTimeOffset>("lastUpdated"),
                Data = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(state, next))
            });
            state = next;
        }
        return result;
    }

    private static Sample MeasureSourceHistory(JObject initial, List<ChangeTrackingEntry> deltas, bool rebase, int rejectEvery)
    {
        var working = (JObject)initial.DeepClone();
        Collect();
        var start = Snapshot();
        var stopwatch = Stopwatch.StartNew();
        var entries = new List<ChangeTrackingEntry>();
        for (var i = 0; i < deltas.Count; i++)
        {
            if (rejectEvery > 0 && (i + 1) % rejectEvery == 0) continue;
            var delta = deltas[i];
            JObject data;
            if (rebase)
            {
                var next = (JObject)ChangeTrackingHelper.JsonDiffPatch.Patch(working.DeepClone(), delta.Data);
                data = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(working, next));
                working = next;
            }
            else data = ChangeTrackingHelper.StripBaseProperties((JObject)delta.Data.DeepClone());
            entries.Add(new ChangeTrackingEntry
            {
                EntryType = delta.EntryType, DataSource = delta.DataSource, EntityType = delta.EntityType,
                EntityId = delta.EntityId, Timestamp = delta.Timestamp, Data = data
            });
        }
        stopwatch.Stop();
        var end = Snapshot();
        if (rebase)
        {
            var replay = ChangeTrackingHelper.ReassembleEntity(new List<ChangeTrackingEntry>
            {
                new() { EntryType = ChangeTrackingEntryTypes.Init, DataSource = "TestSource", EntityType = "Reservation", EntityId = "booking-0",
                    Timestamp = MergeRegressionHarness.SourceTime(0), Data = ChangeTrackingHelper.StripBaseProperties(initial) }
            }.Concat(entries).ToList());
            var finalIndex = Enumerable.Range(1, deltas.Count).Last(i => rejectEvery == 0 || i % rejectEvery != 0);
            replay.Value<decimal>("Total").Should().Be(finalIndex);
            replay.Value<decimal>("Total").Should().Be(working.Value<decimal>("Total"));
            entries.Should().HaveCount(deltas.Count - (rejectEvery > 0 ? deltas.Count / rejectEvery : 0));
        }
        return SampleFrom(stopwatch.Elapsed.TotalMilliseconds, start, end, 0, entries.Count);
    }

    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    private static (long Bytes, int Gen0, int Gen1, int Gen2) Snapshot() => (GC.GetTotalAllocatedBytes(true), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    private static Sample SampleFrom(double milliseconds, (long Bytes, int Gen0, int Gen1, int Gen2) start,
        (long Bytes, int Gen0, int Gen1, int Gen2) end, int writes, int updates) => new(milliseconds, end.Bytes - start.Bytes,
            end.Gen0 - start.Gen0, end.Gen1 - start.Gen1, end.Gen2 - start.Gen2, writes, updates);
    private static double Median(IEnumerable<double> values) => values.Order().ElementAt(Repetitions / 2);
    private sealed record Sample(double Milliseconds, long AllocatedBytes, int Gen0, int Gen1, int Gen2, int ProjectionWrites, int HistoryUpdates);
    private sealed record Measurement(string Scenario, int Changes, double MedianMilliseconds, double MedianAllocatedMiB,
        double MedianGen0, double MedianGen1, double MedianGen2, int ProjectionWrites, int HistoryUpdates, List<Sample> Samples);
}
