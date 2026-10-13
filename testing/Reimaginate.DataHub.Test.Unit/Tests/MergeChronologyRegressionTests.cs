using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

[Trait("Category", "MergeRegression")]
public class MergeChronologyRegressionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Reversed_versions_leave_the_latest_state_for_new_and_existing_entities(bool tracked, bool existing)
    {
        using var db = new MergeRegressionHarness(true);
        if (existing) db.SeedBooking("booking-1", "entity-1");

        var response = await db.MergePublicAsync(tracked,
            [MergeRegressionHarness.Cancelled(), MergeRegressionHarness.Booked()]);

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        var entity = db.Stored.Values.Should().ContainSingle().Which;
        AssertCancelled(entity);
        if (tracked)
        {
            var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
            var destination = db.HistoryFor(DataSources.DataHub, "Booking", entity.Value<string>("id")!);
            foreach (var history in new[] { source, destination })
            {
                history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp)
                    .Should().Equal(existing
                        ? new[] { MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(2) }
                        : new[] { MergeRegressionHarness.SourceTime(2) });
                var initial = history.Single(e => e.EntryType == ChangeTrackingEntryTypes.Init);
                initial.Timestamp.Should().Be(MergeRegressionHarness.SourceTime(0), "initialisation retains the entity's createdOn time");
                initial.Data.Value<decimal>("Total").Should().Be(existing ? 10m : 42m,
                    "a new entity must be initialised from the earliest incoming version");
                AssertCancelled(ChangeTrackingHelper.ReassembleEntity(history));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reversed_interleaved_sources_keep_each_entity_latest_and_preserve_partial_changes(bool tracked)
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        db.SeedBooking("booking-2", "entity-2");
        var earlier = MergeRegressionHarness.Booked();
        earlier.Data["DeliveryNote"] = "Leave at reception";

        var response = await db.MergePublicAsync(tracked,
            [MergeRegressionHarness.Cancelled(), MergeRegressionHarness.Cancelled("booking-2"),
                earlier, MergeRegressionHarness.Booked("booking-2")]);

        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        AssertCancelled(db.Stored["entity-1"]);
        AssertCancelled(db.Stored["entity-2"]);
        db.Stored["entity-1"].Value<string>("DeliveryNote").Should().Be("Leave at reception");
        db.EntityWriteCount("entity-1").Should().Be(1);
        db.EntityWriteCount("entity-2").Should().Be(1);
        if (tracked)
        {
            var history = db.HistoryFor("TestSource", "Reservation", "booking-1");
            history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp)
                .Should().Equal(MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(2));
            ChangeTrackingHelper.ReassembleEntity(history).Value<string>("DeliveryNote").Should().Be("Leave at reception");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Equal_timestamps_preserve_arrival_order_and_individual_changes(bool tracked)
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var latest = MergeRegressionHarness.Cancelled();
        latest.Data["lastUpdated"] = MergeRegressionHarness.SourceTime(1);
        var response = await db.MergePublicAsync(tracked, [MergeRegressionHarness.Booked(), latest]);

        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        AssertCancelled(db.Stored["entity-1"]);
        if (tracked)
        {
            foreach (var history in new[] { db.HistoryFor("TestSource", "Reservation", "booking-1"),
                         db.HistoryFor(DataSources.DataHub, "Booking", "entity-1") })
            {
                history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Should().HaveCount(2)
                    .And.OnlyContain(e => e.Timestamp == MergeRegressionHarness.SourceTime(1));
                AssertCancelled(ChangeTrackingHelper.ReassembleEntity(history));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timestamps_with_different_offsets_are_ordered_by_the_actual_instant(bool tracked)
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var earlier = MergeRegressionHarness.Booked();
        earlier.Data["lastUpdated"] = "2030-01-01T10:01:00+10:00";
        var latest = MergeRegressionHarness.Cancelled();
        latest.Data["lastUpdated"] = "2030-01-01T00:02:00Z";
        var response = await db.MergePublicAsync(tracked, [latest, earlier]);

        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        AssertCancelled(db.Stored["entity-1"]);
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(false, "invalid")]
    [InlineData(true, "missing")]
    [InlineData(true, "invalid")]
    public async Task An_unorderable_version_fails_individually_while_valid_and_independent_updates_continue(bool tracked, string timestamp)
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        db.SeedBooking("booking-2", "entity-2");
        var bad = MergeRegressionHarness.Booked();
        if (timestamp == "missing") bad.Data.Remove("lastUpdated");
        else bad.Data["lastUpdated"] = "not-a-date";

        var response = await db.MergePublicAsync(tracked,
            [MergeRegressionHarness.Cancelled(), bad, MergeRegressionHarness.Cancelled("booking-2")]);

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().ContainSingle(r => r.MergeOutcome == MergeOutcomes.MergeFailed &&
            r.SourceEntityId == "booking-1" && r.FailureReason.Contains("lastUpdated"));
        AssertCancelled(db.Stored["entity-1"]);
        AssertCancelled(db.Stored["entity-2"]);
        if (tracked)
            db.HistoryFor("TestSource", "Reservation", "booking-1").Where(e => e.EntryType == ChangeTrackingEntryTypes.Update)
                .Should().ContainSingle(e => e.Timestamp == MergeRegressionHarness.SourceTime(2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Rejection_in_reversed_input_preserves_each_accepted_transition_and_history_replay(int rejectedVersion)
    {
        using var db = new MergeRegressionHarness(true)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => new CheckPreMergeRulesResponse
            {
                Pass = check.ChangeSet?.Timestamp != MergeRegressionHarness.SourceTime(rejectedVersion),
                Action = PreMergeRuleActions.RejectMerge
            }
        };
        db.SeedBooking("booking-1", "entity-1");
        var inputs = Enumerable.Range(1, 3).Reverse().Select(version =>
        {
            var request = MergeRegressionHarness.Booked(version: version);
            request.Data["Total"] = version;
            return request;
        }).ToList();
        var response = await db.MergePublicAsync(true, inputs);

        response.Results.Should().ContainSingle(r => r.MergeOutcome == MergeOutcomes.MergeRejected);
        var accepted = Enumerable.Range(1, 3).Where(version => version != rejectedVersion).ToArray();
        foreach (var history in new[] { db.HistoryFor("TestSource", "Reservation", "booking-1"),
                     db.HistoryFor(DataSources.DataHub, "Booking", "entity-1") })
        {
            history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp)
                .Should().Equal(accepted.Select(MergeRegressionHarness.SourceTime));
            ChangeTrackingHelper.ReassembleEntity(history).Value<int>("Total").Should().Be(accepted.Last());
        }
        db.Stored["entity-1"].Value<int>("Total").Should().Be(accepted.Last());
    }

    private static void AssertCancelled(JObject entity)
    {
        entity.Value<decimal>("Total").Should().Be(0);
        entity.SelectToken("BookingLines[0].Status")!.Value<string>().Should().Be("Cancelled");
    }
}
