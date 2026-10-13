using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.SharedModels.Core;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

[Trait("Category", "MergeRegression")]
public class MergePersistenceRegressionTests
{
    // Chronological and reversed inputs must produce the same final source version,
    // independently of the database write completion schedule.
    public static TheoryData<bool, bool> PathsAndSchedules => new()
    {
        { false, false }, { false, true }, { true, false }, { true, true }
    };

    [Theory]
    [MemberData(nameof(PathsAndSchedules))]
    public async Task Latest_ordered_update_survives_either_database_completion_order(bool tracked, bool newestWriteFirst)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst);
        db.SeedBooking("booking-1", "entity-1");

        var results = await db.MergeUpdatesAsync(tracked,
            [MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled()]);

        AssertCancellation(db.Stored["entity-1"]);
        db.EntityWriteCount("entity-1").Should().Be(1, "only the final current entity should be written");
        results.Should().NotBeEmpty().And.NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        if (tracked)
        {
            var replayed = ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1"));
            AssertCancellation(replayed);
        }
    }

    [Theory]
    [MemberData(nameof(PathsAndSchedules))]
    public async Task Public_merge_preserves_the_latest_update_and_notifies_confirmed_persistence(bool tracked, bool newestWriteFirst)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst);
        db.SeedBooking("booking-1", "entity-1");

        var response = await db.MergePublicAsync(tracked,
            [MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled()]);

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        AssertCancellation(db.Stored["entity-1"]);
        db.Notifications.OfType<DataHubEntityUpdatedNotification>().Should().NotBeEmpty()
            .And.OnlyContain(n => n.DataHubEntityId == "entity-1");
        db.NotificationObservations.Should().OnlyContain(o => o.Confirmed,
            "an entity notification must follow a successful database write");
    }

    [Theory]
    [MemberData(nameof(PathsAndSchedules))]
    public async Task Three_updates_remain_deterministic_while_an_independent_entity_progresses(bool tracked, bool newestWriteFirst)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst) { HoldEntityUntilAnotherCompletes = "entity-1" };
        db.SeedBooking("booking-1", "entity-1");
        db.SeedBooking("booking-2", "entity-2");
        var middle = MergeRegressionHarness.Booked(version: 2);
        middle.Data["Total"] = 21m;

        var results = await db.MergeUpdatesAsync(tracked,
            [MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled("booking-2"), middle,
                MergeRegressionHarness.Cancelled(version: 3)]);

        AssertCancellation(db.Stored["entity-1"], version: 3);
        AssertCancellation(db.Stored["entity-2"]);
        db.EntityWriteCount("entity-1").Should().Be(1);
        db.EntityWriteCount("entity-2").Should().Be(1);
        results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        db.CompletedWrites.First().EntityId.Should().Be("entity-2");
        db.CompletedWrites.First().OtherEntityWasPending.Should().BeTrue(
            "another entity must be able to persist while the first entity is held at the database boundary");
    }

    [Theory]
    [MemberData(nameof(PathsAndSchedules))]
    public async Task New_entity_and_updates_in_one_public_request_produce_one_current_entity(bool tracked, bool newestWriteFirst)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst);

        var response = await db.MergePublicAsync(tracked,
            [MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled()]);

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        var entity = db.Stored.Values.Should().ContainSingle().Which;
        AssertCancellation(entity);
        entity.Value<string>("id").Should().NotBe("booking-1", "source and DataHub identities are distinct");
        entity["alternateKeys"]!.ToObject<List<Reimaginate.DataHub.SharedModels.Core.AlternateKey>>()!
            .Should().ContainSingle(k => k.Key == "testsource.reservation" && k.Value == "booking-1");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_write_is_visible_and_never_produces_a_success_notification(bool tracked)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: true) { FailEntity = "entity-1" };
        db.SeedBooking("booking-1", "entity-1");
        db.SeedBooking("booking-2", "entity-2");

        var response = await db.MergePublicAsync(tracked,
            [MergeRegressionHarness.Cancelled(), MergeRegressionHarness.Cancelled("booking-2")]);

        using var scope = new AssertionScope();
        AssertCancellation(db.Stored["entity-2"]);
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(10m, "the failed write cannot change the stored document");
        (response.Success && !(response.Results?.Any(r => r.SourceEntityId == "booking-1" && MergeOutcomes.IsFailure(r.MergeOutcome)) ?? false))
            .Should().BeFalse("failure must be visible in the request outcome or per-entity result");
        response.Results?.Should().NotContain(r => r.SourceEntityId == "booking-1" &&
            (r.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated || r.MergeOutcome == MergeOutcomes.NewEntityCreated));
        db.Notifications.OfType<DataHubEntityUpdatedNotification>().Should().NotContain(n => n.DataHubEntityId == "entity-1");
        db.Notifications.OfType<DataHubEntityCreatedNotification>().Should().NotContain(n => n.DataHubEntityId == "entity-1");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordered_partial_updates_preserve_fields_from_an_earlier_update(bool tracked)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: false);
        db.SeedBooking("booking-1", "entity-1");
        var earlier = MergeRegressionHarness.Booked();
        earlier.Data["DeliveryNote"] = "Leave at reception";

        await db.MergeUpdatesAsync(tracked, [earlier, MergeRegressionHarness.Cancelled()]);

        AssertCancellation(db.Stored["entity-1"]);
        db.Stored["entity-1"].Value<string>("DeliveryNote").Should().Be("Leave at reception",
            "omitting a field from a later partial update must not undo an earlier update");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Single_public_update_persists_and_emits_a_confirmed_notification(bool tracked)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: false);
        db.SeedBooking("booking-1", "entity-1");

        var response = await db.MergePublicAsync(tracked, [MergeRegressionHarness.Cancelled()]);

        response.Success.Should().BeTrue(response.FailureReason);
        AssertCancellation(db.Stored["entity-1"]);
        db.Notifications.OfType<DataHubEntityUpdatedNotification>().Should().ContainSingle();
        db.NotificationObservations.Should().OnlyContain(o => o.Confirmed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_tracked_merge_keeps_each_change_at_its_source_time(bool newestWriteFirst)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst);
        db.SeedBooking("booking-1", "entity-1");
        var middle = MergeRegressionHarness.Booked(version: 2);
        middle.Data["Total"] = 21m;

        var response = await db.MergePublicAsync(true,
            [MergeRegressionHarness.Booked(), middle, MergeRegressionHarness.Cancelled(version: 3)]);

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        db.EntityWriteCount("entity-1").Should().Be(1);
        foreach (var (source, type, id) in new[]
                 { (DataSources.DataHub, "Booking", "entity-1"), ("TestSource", "Reservation", "booking-1") })
        {
            var history = db.HistoryFor(source, type, id);
            history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp)
                .Should().Equal(Enumerable.Range(1, 3).Select(MergeRegressionHarness.SourceTime));
            for (var version = 1; version <= 3; version++)
            {
                var atTime = ChangeTrackingHelper.ReassembleEntity(history.Where(e =>
                    e.EntryType == ChangeTrackingEntryTypes.Init || e.Timestamp <= MergeRegressionHarness.SourceTime(version)).ToList());
                atTime.Value<decimal>("Total").Should().Be(version switch { 1 => 42m, 2 => 21m, _ => 0m });
                atTime.SelectToken("BookingLines[0].Status")!.Value<string>()
                    .Should().Be(version == 3 ? "Cancelled" : "Booked");
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_tracked_reversal_retains_both_changes_even_with_no_net_business_change(bool newestWriteFirst)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst);
        db.SeedBooking("booking-1", "entity-1");
        var original = (JObject)db.Stored["entity-1"].DeepClone();
        var reversal = MergeRegressionHarness.Cancelled();
        reversal.Data["Total"] = original["Total"]!.DeepClone();
        reversal.Data["BookingLines"] = original["BookingLines"]!.DeepClone();

        var response = await db.MergePublicAsync(true, [MergeRegressionHarness.Booked(), reversal]);

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        db.EntityWriteCount("entity-1").Should().Be(1);
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(original.Value<decimal>("Total"));
        JToken.DeepEquals(db.Stored["entity-1"]["BookingLines"], original["BookingLines"]).Should().BeTrue();
        foreach (var (source, type, id) in new[]
                 { (DataSources.DataHub, "Booking", "entity-1"), ("TestSource", "Reservation", "booking-1") })
        {
            var history = db.HistoryFor(source, type, id);
            var updates = history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).ToList();
            updates.Select(e => e.Timestamp).Should().Equal(MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(2));
            ChangeTrackingHelper.ReassembleEntity(history.Where(e => e.Timestamp <= MergeRegressionHarness.SourceTime(1)).ToList())
                .Value<decimal>("Total").Should().Be(42m);
            ChangeTrackingHelper.ReassembleEntity(history).Value<decimal>("Total").Should().Be(10m);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_partial_updates_preserve_the_earlier_field_and_its_history(bool tracked)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: true);
        db.SeedBooking("booking-1", "entity-1");
        var earlier = MergeRegressionHarness.Booked();
        earlier.Data["DeliveryNote"] = "Leave at reception";

        var response = await db.MergePublicAsync(tracked, [earlier, MergeRegressionHarness.Cancelled()]);

        response.Success.Should().BeTrue(response.FailureReason);
        AssertCancellation(db.Stored["entity-1"]);
        db.Stored["entity-1"].Value<string>("DeliveryNote").Should().Be("Leave at reception");
        db.EntityWriteCount("entity-1").Should().Be(1);
        if (tracked)
        {
            var history = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1");
            history.Single(e => e.EntryType == ChangeTrackingEntryTypes.Update && e.Timestamp == MergeRegressionHarness.SourceTime(1))
                .Data.Should().ContainKey("DeliveryNote");
            ChangeTrackingHelper.ReassembleEntity(history).Value<string>("DeliveryNote").Should().Be("Leave at reception");
        }
    }

    [Fact]
    public async Task Repeated_tracked_alternate_key_updates_use_each_input_and_preserve_history()
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: true);
        db.SeedBooking("booking-1", "entity-1");
        var first = MergeRegressionHarness.Booked();
        first.Data["alternateKeys"] = JArray.FromObject(new[] { new AlternateKey("customer.account", "account-1") });
        var second = MergeRegressionHarness.Cancelled();
        second.Data["alternateKeys"] = JArray.FromObject(new[] { new AlternateKey("customer.account", "account-2") });

        var response = await db.MergePublicAsync(tracked: true, [first, second]);

        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        db.EntityWriteCount("entity-1").Should().Be(1);
        KeyValue(db.Stored["entity-1"]).Should().Be("account-2");
        var history = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1");
        KeyValue(ChangeTrackingHelper.ReassembleEntity(history.Where(e => e.Timestamp <= MergeRegressionHarness.SourceTime(1)).ToList()))
            .Should().Be("account-1");
        KeyValue(ChangeTrackingHelper.ReassembleEntity(history)).Should().Be("account-2");
        history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update && e.Data.ContainsKey("alternateKeys"))
            .Select(e => e.Timestamp).Should().Equal(MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(2));

        static string? KeyValue(JObject entity) => entity["alternateKeys"]!.ToObject<List<AlternateKey>>()!
            .Single(k => k.Key == "customer.account").Value;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Merge_rules_observe_the_latest_accepted_working_entity(bool tracked)
    {
        var observedTotals = new List<decimal>();
        using var db = new MergeRegressionHarness(newestWriteFirst: true)
        {
            Config = new EntityConfig(),
            PreMergeRule = request =>
            {
                observedTotals.Add(request.ExistingEntity.Value<decimal>("Total"));
                return new CheckPreMergeRulesResponse { Pass = true };
            }
        };
        db.SeedBooking("booking-1", "entity-1");
        var middle = MergeRegressionHarness.Booked(version: 2);
        middle.Data["Total"] = 21m;

        await db.MergeUpdatesAsync(tracked,
            [MergeRegressionHarness.Booked(), middle, MergeRegressionHarness.Cancelled(version: 3)]);

        observedTotals.Should().Equal(10m, 42m, 21m);
        AssertCancellation(db.Stored["entity-1"], version: 3);
        db.EntityWriteCount("entity-1").Should().Be(1);
    }

    [Fact]
    public async Task Rejected_tracked_update_does_not_discard_other_accepted_history_for_the_entity()
    {
        var attempt = 0;
        using var db = new MergeRegressionHarness(newestWriteFirst: true)
        {
            Config = new EntityConfig(),
            PreMergeRule = _ => ++attempt == 2
                ? new CheckPreMergeRulesResponse { Pass = false, Action = PreMergeRuleActions.RejectMerge }
                : new CheckPreMergeRulesResponse { Pass = true }
        };
        db.SeedBooking("booking-1", "entity-1");
        var middle = MergeRegressionHarness.Booked(version: 2);
        middle.Data["Total"] = 21m;

        var results = await db.MergeUpdatesAsync(true,
            [MergeRegressionHarness.Booked(), middle, MergeRegressionHarness.Cancelled(version: 3)]);

        results.Should().Contain(r => r.MergeOutcome == MergeOutcomes.MergeRejected);
        var history = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1");
        var updates = history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).ToList();
        updates.Select(e => e.Timestamp).Should().Equal(MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(3));
        updates[1].Data["Total"]![0]!.Value<decimal>().Should().Be(42m,
            "the destination delta must describe the previous accepted value, not the rejected source value");
        AssertCancellation(ChangeTrackingHelper.ReassembleEntity(history), version: 3);
        AssertCancellation(db.Stored["entity-1"], version: 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repeated_entity_across_internal_batch_boundary_preserves_order_and_partial_fields(bool tracked)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: true);
        db.SeedBooking("booking-1", "entity-1");
        var earlier = MergeRegressionHarness.Booked();
        earlier.Data["DeliveryNote"] = "Leave at reception";
        var requests = Enumerable.Repeat(earlier, 5000).Append(MergeRegressionHarness.Cancelled()).ToList();

        var response = await db.MergePublicAsync(tracked, requests);

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        AssertCancellation(db.Stored["entity-1"]);
        db.Stored["entity-1"].Value<string>("DeliveryNote").Should().Be("Leave at reception");
        db.EntityWriteCount("entity-1").Should().Be(2, "each of the two sequential persistence units writes the entity once");
        if (tracked)
        {
            db.HistoryFor("TestSource", "Reservation", "booking-1")
                .Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp)
                .Should().Equal(MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(2));
        }
    }

    [Fact]
    public async Task Public_rejected_tracked_update_preserves_other_accepted_source_history()
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: true)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => check.ChangeSet?.Timestamp == MergeRegressionHarness.SourceTime(2)
                ? new CheckPreMergeRulesResponse { Pass = false, Action = PreMergeRuleActions.RejectMerge }
                : new CheckPreMergeRulesResponse { Pass = true }
        };
        db.SeedBooking("booking-1", "entity-1");
        var middle = MergeRegressionHarness.Booked(version: 2);
        middle.Data["Total"] = 21m;

        var response = await db.MergePublicAsync(true,
            [MergeRegressionHarness.Booked(), middle, MergeRegressionHarness.Cancelled(version: 3)]);

        response.Results.Should().Contain(r => r.MergeOutcome == MergeOutcomes.MergeRejected);
        AssertCancellation(db.Stored["entity-1"], version: 3);
        var destinationHistory = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1");
        destinationHistory.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp)
            .Should().Equal(MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(3));
        using var scope = new AssertionScope();
        var sourceHistory = db.HistoryFor("TestSource", "Reservation", "booking-1");
        sourceHistory.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp)
            .Should().Equal([MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(3)],
                "rejecting one change must not discard the other accepted source observations");
        AssertCancellation(ChangeTrackingHelper.ReassembleEntity(sourceHistory), version: 3);

        var later = MergeRegressionHarness.Booked(version: 4);
        later.Data["Total"] = 10m;
        await db.MergePublicAsync(true, [later]);
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(10m,
            "a later change back to the initial value must be compared with the last accepted source state");
    }

    [Fact]
    public async Task Repeated_previously_untracked_source_is_initialised_once_then_records_later_changes()
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: true);
        db.SeedBooking("booking-1", "entity-1");
        db.ForgetSourceHistory("booking-1");

        var response = await db.MergePublicAsync(true,
            [MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled()]);

        response.Success.Should().BeTrue(response.FailureReason);
        AssertCancellation(db.Stored["entity-1"]);
        var history = db.HistoryFor("TestSource", "Reservation", "booking-1");
        history.Should().ContainSingle(e => e.EntryType == ChangeTrackingEntryTypes.Init && e.Timestamp == MergeRegressionHarness.SourceTime(1));
        history.Should().ContainSingle(e => e.EntryType == ChangeTrackingEntryTypes.Update && e.Timestamp == MergeRegressionHarness.SourceTime(2));
        AssertCancellation(ChangeTrackingHelper.ReassembleEntity(history));
    }

    private static void AssertCancellation(JObject entity, int version = 2)
    {
        using var scope = new AssertionScope();
        entity.Value<decimal>("Total").Should().Be(0);
        entity.SelectToken("BookingLines[0].Status")!.Value<string>().Should().Be("Cancelled");
        entity.SelectToken("BookingLines[0].Updated")!.AsDateTimeOffset().Should().Be(MergeRegressionHarness.SourceTime(version));
    }
}
