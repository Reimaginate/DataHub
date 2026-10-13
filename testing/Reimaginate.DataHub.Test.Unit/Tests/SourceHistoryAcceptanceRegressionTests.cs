using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

[Trait("Category", "MergeRegression")]
public class SourceHistoryAcceptanceRegressionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Hard_rejection_preserves_other_changes_and_their_original_timestamps(int rejectedVersion)
    {
        using var db = new MergeRegressionHarness(true)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => Rule(check.ChangeSet?.Timestamp == MergeRegressionHarness.SourceTime(rejectedVersion))
        };
        db.SeedBooking("booking-1", "entity-1");
        var inputs = Enumerable.Range(1, 3).Select(v => MergeRegressionHarness.Booked(version: v)).ToList();
        foreach (var input in inputs) input.Data["Total"] = input.Data.Value<DateTimeOffset>("lastUpdated").Minute;
        var response = await db.MergePublicAsync(true, inputs);

        response.Results.Should().ContainSingle(r => r.MergeOutcome == MergeOutcomes.MergeRejected);
        var expectedTimes = Enumerable.Range(1, 3).Where(v => v != rejectedVersion).Select(MergeRegressionHarness.SourceTime).ToArray();
        var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
        var destination = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1");
        source.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp).Should().Equal(expectedTimes);
        destination.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp).Should().Equal(expectedTimes);
        var lastAccepted = Enumerable.Range(1, 3).Last(v => v != rejectedVersion);
        ChangeTrackingHelper.ReassembleEntity(source).Value<int>("Total").Should().Be(lastAccepted);
        ChangeTrackingHelper.ReassembleEntity(destination).Value<int>("Total").Should().Be(lastAccepted);
        db.Stored["entity-1"].Value<int>("Total").Should().Be(lastAccepted);
        db.EntityWriteCount("entity-1").Should().Be(1);
        if (rejectedVersion < 3)
            source.Last().Data["Total"]![0]!.Value<int>().Should().Be(rejectedVersion == 1 ? 2 : 1,
                "rebased history must use the previous accepted value, not the rejected value");
    }

    [Fact]
    public async Task Equal_timestamps_do_not_conflate_rejected_and_accepted_inputs()
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => Rule(check.IncomingEntity["Total"]?.Last?.Value<decimal>() == 21m)
        };
        db.SeedBooking("booking-1", "entity-1");
        var inputs = new[] { MergeRegressionHarness.Booked(), MergeRegressionHarness.Booked(version: 2), MergeRegressionHarness.Cancelled(version: 3) };
        inputs[1].Data["Total"] = 21m;
        foreach (var input in inputs) input.Data["lastUpdated"] = MergeRegressionHarness.SourceTime(1);
        var response = await db.MergePublicAsync(true, inputs.ToList());

        response.Results.Should().ContainSingle(r => r.MergeOutcome == MergeOutcomes.MergeRejected);
        var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
        source.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Should().HaveCount(2)
            .And.OnlyContain(e => e.Timestamp == MergeRegressionHarness.SourceTime(1));
        ChangeTrackingHelper.ReassembleEntity(source).Value<int>("Total").Should().Be(0);
        db.Stored["entity-1"].Value<int>("Total").Should().Be(0);
    }

    [Fact]
    public async Task Rebasing_a_partial_update_does_not_resurrect_an_unrelated_rejected_property()
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => Rule(check.ChangeSet?.Timestamp == MergeRegressionHarness.SourceTime(2))
        };
        db.SeedBooking("booking-1", "entity-1");
        var rejected = MergeRegressionHarness.Booked(version: 2);
        rejected.Data = new JObject { ["RejectedOnly"] = "blocked", ["lastUpdated"] = MergeRegressionHarness.SourceTime(2) };
        var accepted = MergeRegressionHarness.Booked(version: 3);
        accepted.Data = new JObject { ["Total"] = 0, ["lastUpdated"] = MergeRegressionHarness.SourceTime(3) };
        await db.MergePublicAsync(true, [MergeRegressionHarness.Booked(), rejected, accepted]);

        var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
        var replay = ChangeTrackingHelper.ReassembleEntity(source);
        replay["RejectedOnly"].Should().BeNull();
        replay.Value<int>("Total").Should().Be(0);
        db.Stored["entity-1"]["RejectedOnly"].Should().BeNull();
        var later = MergeRegressionHarness.Booked(version: 4);
        later.Data = new JObject { ["Total"] = 10, ["lastUpdated"] = MergeRegressionHarness.SourceTime(4) };
        await db.MergePublicAsync(true, [later]);
        db.Stored["entity-1"].Value<int>("Total").Should().Be(10);
    }

    [Fact]
    public async Task A_single_rejected_source_does_not_suppress_an_independent_accepted_source()
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => Rule(check.ExistingEntity.Value<string>("id") == "entity-1")
        };
        db.SeedBooking("booking-1", "entity-1");
        db.SeedBooking("booking-2", "entity-2");
        await db.MergePublicAsync(true, [MergeRegressionHarness.Booked(), MergeRegressionHarness.Booked("booking-2")]);

        db.HistoryFor("TestSource", "Reservation", "booking-1").Should().ContainSingle();
        var source = db.HistoryFor("TestSource", "Reservation", "booking-2");
        source.Should().HaveCount(2);
        ChangeTrackingHelper.ReassembleEntity(source).Value<int>("Total").Should().Be(42);
        db.Stored["entity-1"].Value<int>("Total").Should().Be(10);
        db.Stored["entity-2"].Value<int>("Total").Should().Be(42);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_skipped_change_does_not_discard_other_source_history(bool throws)
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => check.ChangeSet?.Timestamp != MergeRegressionHarness.SourceTime(2)
                ? new CheckPreMergeRulesResponse { Pass = true }
                : throws ? throw new InvalidOperationException("Injected per-change processing failure") : Rule(true)
        };
        db.SeedBooking("booking-1", "entity-1");
        await db.MergePublicAsync(true, [MergeRegressionHarness.Booked(), MergeRegressionHarness.Booked(version: 2), MergeRegressionHarness.Cancelled(version: 3)]);
        db.HistoryFor("TestSource", "Reservation", "booking-1").Where(e => e.EntryType == ChangeTrackingEntryTypes.Update)
            .Select(e => e.Timestamp).Should().Equal(MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(3));
    }

    [Fact]
    public async Task Silent_rejection_keeps_the_source_observation_and_only_skips_destination_history()
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => check.ChangeSet?.Timestamp == MergeRegressionHarness.SourceTime(2)
                ? new CheckPreMergeRulesResponse { Pass = false, Action = PreMergeRuleActions.SilentlyRejectMerge }
                : new CheckPreMergeRulesResponse { Pass = true }
        };
        db.SeedBooking("booking-1", "entity-1");
        await db.MergePublicAsync(true, [MergeRegressionHarness.Booked(), MergeRegressionHarness.Booked(version: 2), MergeRegressionHarness.Cancelled(version: 3)]);
        db.HistoryFor("TestSource", "Reservation", "booking-1").Where(e => e.EntryType == ChangeTrackingEntryTypes.Update)
            .Select(e => e.Timestamp).Should().Equal(Enumerable.Range(1, 3).Select(MergeRegressionHarness.SourceTime));
        db.HistoryFor(DataSources.DataHub, "Booking", "entity-1").Where(e => e.EntryType == ChangeTrackingEntryTypes.Update)
            .Select(e => e.Timestamp).Should().Equal(MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(3));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task History_write_failures_are_reported(bool destinationHistory)
    {
        using var db = new MergeRegressionHarness(false)
        {
            FailHistoryWrite = entry => entry.EntryType == ChangeTrackingEntryTypes.Update &&
                entry.DataSource == (destinationHistory ? DataSources.DataHub : "TestSource")
        };
        db.SeedBooking("booking-1", "entity-1");
        var response = await db.MergePublicAsync(true, [MergeRegressionHarness.Booked()]);
        if (destinationHistory)
        {
            response.Success.Should().BeFalse();
            response.FailureReason.Should().Contain("Injected history write failure");
            db.EntityWriteCount("entity-1").Should().Be(0);
        }
        else response.Results.Should().Contain(r => r.MergeOutcome == MergeOutcomes.MergeFailed && r.FailureReason.Contains("Injected history write failure"));
    }

    [Fact]
    public async Task A_failed_final_projection_does_not_advance_source_history()
    {
        using var db = new MergeRegressionHarness(false) { FailEntity = "entity-1" };
        db.SeedBooking("booking-1", "entity-1");
        var response = await db.MergePublicAsync(true, [MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled()]);
        response.Results.Should().Contain(r => r.MergeOutcome == MergeOutcomes.MergeFailed);
        db.HistoryFor("TestSource", "Reservation", "booking-1").Should().ContainSingle();
    }

    [Fact]
    public async Task Rejecting_every_change_leaves_the_existing_projection_and_histories_unchanged()
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(), PreMergeRule = _ => Rule(true)
        };
        db.SeedBooking("booking-1", "entity-1");
        await db.MergePublicAsync(true, [MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled()]);
        db.Stored["entity-1"].Value<int>("Total").Should().Be(10);
        db.EntityWriteCount("entity-1").Should().Be(0);
        db.HistoryFor("TestSource", "Reservation", "booking-1").Should().ContainSingle();
        db.HistoryFor(DataSources.DataHub, "Booking", "entity-1").Should().ContainSingle();
    }

    [Fact]
    public async Task Rebased_object_and_array_changes_record_the_previous_accepted_values()
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => Rule(check.ChangeSet?.Timestamp == MergeRegressionHarness.SourceTime(2))
        };
        db.SeedBooking("booking-1", "entity-1");
        var inputs = Enumerable.Range(1, 3).Select(v => MergeRegressionHarness.Booked(version: v)).ToList();
        inputs[0].Data["Customer"] = new JObject { ["Name"] = "Accepted" };
        inputs[1].Data["Customer"] = null;
        inputs[2].Data["Customer"] = new JObject { ["Name"] = "Later accepted" };
        await db.MergePublicAsync(true, inputs);
        var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
        var replay = ChangeTrackingHelper.ReassembleEntity(source);
        replay.SelectToken("Customer.Name")!.Value<string>().Should().Be("Later accepted");
        replay.SelectToken("BookingLines[0].Updated")!.AsDateTimeOffset().Should().Be(MergeRegressionHarness.SourceTime(3));
        var lastDelta = source.Last().Data;
        lastDelta.SelectToken("Customer.Name[0]")!.Value<string>().Should().Be("Accepted");
        lastDelta.SelectToken("BookingLines[0][0].Updated")!.AsDateTimeOffset().Should().Be(MergeRegressionHarness.SourceTime(1));
    }

    private static CheckPreMergeRulesResponse Rule(bool reject) => reject
        ? new() { Pass = false, Action = PreMergeRuleActions.RejectMerge }
        : new() { Pass = true };
}
