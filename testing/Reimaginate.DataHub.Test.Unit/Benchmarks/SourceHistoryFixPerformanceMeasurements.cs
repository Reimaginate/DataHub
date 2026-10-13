using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.Test.Unit.Tests;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Benchmarks;

// Opt-in local benchmark; use the same test assembly/harness with the before/after
// runtime DLL. No timers or assertions are added to production processing.
public class SourceHistoryFixPerformanceMeasurements(ITestOutputHelper output)
{
    [Fact]
    public async Task Measure_actual_source_history_fix()
    {
        var results = new List<object>();
        await Run(20, 1, false); // Exclude first-use/JIT from the samples.
        foreach (var scenario in new[] { (100, 1, false), (1000, 1, false), (5000, 1, false),
                     (1000, 2, false), (5000, 2, false), (999, 3, true), (4998, 3, true),
                     (100, 100, false), (100, 100, true) })
        {
            var samples = new List<Sample>();
            for (var i = 0; i < 3; i++) samples.Add(await Run(scenario.Item1, scenario.Item2, scenario.Item3));
            double Median(Func<Sample, double> value) => samples.Select(value).Order().ElementAt(1);
            var result = new
            {
                Changes = scenario.Item1, UpdatesPerEntity = scenario.Item2, Rejections = scenario.Item3,
                MedianMilliseconds = Median(s => s.Milliseconds), MedianAllocatedMiB = Median(s => s.AllocatedMiB),
                MedianPeakManagedHeapMiB = Median(s => s.PeakManagedHeapMiB), MedianPeakWorkingSetMiB = Median(s => s.PeakWorkingSetMiB),
                MedianRetainedManagedHeapMiB = Median(s => s.RetainedManagedHeapMiB),
                SourceHistoryUpdates = samples[0].SourceHistoryUpdates, Samples = samples
            };
            results.Add(result);
            output.WriteLine(JsonSerializer.Serialize(result));
        }
        var label = Environment.GetEnvironmentVariable("DATAHUB_PERF_LABEL") ?? "current";
        var directory = Path.GetFullPath("TestResults/performance");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"source-history-fix-{label}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            RuntimeAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(typeof(Reimaginate.DataHub.Requests.Internal.MergeExistingEntities.MergeExistingEntitiesRequestHandler).Assembly.Location))),
            Label = label, TimestampUtc = DateTimeOffset.UtcNow,
            Notes = new[]
            {
                "Release; three repetitions per case; indexed in-memory reads/immediate writes; no Cosmos/network latency.",
                "Fixture setup and post-run assertions excluded. Allocation counts include the harness and memory sampler.",
                "Peak managed heap and process working set sampled about every 2ms, including endpoints; short-lived peaks may be missed.",
                "Managed heap includes uncollected garbage, not just live objects. Working set includes runtime/native memory; the process is reused between samples.",
                "Retained managed heap is measured after a full collection outside the timed/allocated operation, with fixture, inputs and response kept alive in both runs.",
                "Three-update groups reject the middle update of every fifth source. One repeated source rejects every fifth update.",
                "Source-history counts are reported for both runtimes; final projections are checked in both runs. Permanent integrity tests check accepted observations and transitions."
            }, Results = results
        }, new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        output.WriteLine($"Saved measurements: {path}");
    }

    private static async Task<Sample> Run(int count, int updatesPerEntity, bool reject)
    {
        bool Reject(CheckPreMergeRulesRequest check) => reject && (updatesPerEntity == count
            ? (check.ChangeSet!.Timestamp - MergeRegressionHarness.SourceTime(0)).TotalMinutes % 5 == 0
            : check.ChangeSet?.Timestamp == MergeRegressionHarness.SourceTime(2) &&
              int.Parse(check.ExistingEntity.Value<string>("id")!.Split('-').Last()) % 5 == 0);
        using var db = new MergeRegressionHarness(false)
        {
            PerformanceMode = true, OperationTimeout = TimeSpan.FromSeconds(60),
            Config = reject ? new EntityConfig() : null,
            PreMergeRule = check => Reject(check) ? new() { Pass = false, Action = PreMergeRuleActions.RejectMerge } : new() { Pass = true }
        };
        var requests = Enumerable.Range(0, count).Select(index =>
        {
            var version = index % updatesPerEntity + 1;
            var request = MergeRegressionHarness.Booked("booking-" + index / updatesPerEntity, version);
            request.Data["Total"] = version;
            request.Data["Customer"] = new JObject { ["Name"] = "Generated customer", ["Email"] = "example@example.invalid", ["Reference"] = "customer-1" };
            request.Data["BookingLines"] = new JArray(Enumerable.Range(0, 10).Select(line => new JObject
            {
                ["Ref"] = "line-" + line, ["Status"] = "Version-" + version, ["Updated"] = MergeRegressionHarness.SourceTime(version),
                ["Quantity"] = 1, ["Price"] = 10m
            }));
            return request;
        }).ToList();
        var ids = requests.Select(r => r.SourceEntityId).Distinct().ToArray();
        foreach (var id in ids) db.SeedBooking(id, "entity-" + id);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        using var process = Process.GetCurrentProcess();
        var peakHeap = GC.GetTotalMemory(false);
        var startHeap = peakHeap;
        process.Refresh();
        var peakWorkingSet = process.WorkingSet64;
        var startWorkingSet = peakWorkingSet;
        void SampleMemory()
        {
            // Timer callbacks can overlap. Process.Refresh and its cached properties
            // must be sampled under the same lock, including the final endpoint.
            lock (process)
            {
                var heap = GC.GetTotalMemory(false);
                process.Refresh();
                var workingSet = process.WorkingSet64;
                peakHeap = Math.Max(peakHeap, heap);
                peakWorkingSet = Math.Max(peakWorkingSet, workingSet);
            }
        }
        var startAllocated = GC.GetTotalAllocatedBytes(true);
        using var timer = new Timer(_ => SampleMemory(), null, 2, 2);
        var stopwatch = Stopwatch.StartNew();
        var response = await db.MergePublicAsync(true, requests);
        stopwatch.Stop();
        await timer.DisposeAsync();
        SampleMemory();
        var allocated = GC.GetTotalAllocatedBytes(true) - startAllocated;
        var retainedHeap = GC.GetTotalMemory(true);
        GC.KeepAlive(requests);
        GC.KeepAlive(response);
        GC.KeepAlive(db);
        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(r => r.MergeOutcome == MergeOutcomes.MergeFailed);
        foreach (var id in ids)
        {
            var expected = updatesPerEntity == count && reject && count % 5 == 0 ? count - 1 : updatesPerEntity;
            db.Stored["entity-" + id].Value<int>("Total").Should().Be(expected);
            db.EntityWriteCount("entity-" + id).Should().Be(1);
        }
        var sourceUpdates = ids.Sum(id => db.HistoryFor("TestSource", "Reservation", id).Count(e => e.EntryType == ChangeTrackingEntryTypes.Update));
        return new Sample(stopwatch.Elapsed.TotalMilliseconds, allocated / 1048576d, startHeap / 1048576d,
            peakHeap / 1048576d, startWorkingSet / 1048576d, peakWorkingSet / 1048576d, sourceUpdates, retainedHeap / 1048576d);
    }

    private sealed record Sample(double Milliseconds, double AllocatedMiB, double StartManagedHeapMiB,
        double PeakManagedHeapMiB, double StartWorkingSetMiB, double PeakWorkingSetMiB, int SourceHistoryUpdates, double RetainedManagedHeapMiB);
}
