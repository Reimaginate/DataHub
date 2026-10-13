using System.Globalization;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataHub.SharedModels.Rules;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

// Public production handlers, source deltas, history replay and projection persistence
// execute in process. Cosmos, locks, pre-merge approval and delivery are boundaries.
// These are release gates: known defects must fail rather than be skipped or accepted.
[Trait("Category", "MergeIntegrity")]
public class MergeEventIntegrityRegressionTests
{
    [Fact]
    public async Task Key_only_changes_require_a_destination_checkpoint_before_accepting_history()
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        db.ForgetDestinationHistory("entity-1");
        var input = MergeRegressionHarness.Booked();
        input.Data = new JObject
        {
            ["lastUpdated"] = MergeRegressionHarness.SourceTime(1),
            ["alternateKeys"] = new JArray(new JObject { ["Key"] = "customer.account", ["Value"] = "new-account" })
        };
        var response = await db.MergePublicAsync(true, [input]);
        Assert.Single(response.Results, r => r.MergeOutcome == MergeOutcomes.MergeFailed);
        Assert.Empty(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1"));
        Assert.Single(db.HistoryFor("TestSource", "Reservation", "booking-1"));
        Assert.Equal(0, db.EntityWriteCount("entity-1"));
        Assert.DoesNotContain(db.Stored["entity-1"]["alternateKeys"]!, k => k.Value<string>("Key") == "customer.account");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rebasing_a_full_snapshot_does_not_overwrite_an_unrelated_value_maintained_by_another_source(bool includeKeys)
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => check.ChangeSet?.Timestamp == MergeRegressionHarness.SourceTime(2)
                ? new() { Pass = false, Action = PreMergeRuleActions.RejectMerge }
                : new() { Pass = true }
        };
        db.SeedBooking("booking-1", "entity-1");
        db.SeedSourceAlias("other-1", "entity-1", dataSource: "OtherSource");
        var first = MergeRegressionHarness.Booked();
        first.Data["Customer"] = new JObject { ["Name"] = "Source name", ["Email"] = "first@example.test" };
        if (includeKeys) first.Data["alternateKeys"] = new JArray(new JObject { ["Key"] = "customer.account", ["Value"] = "source-account" });
        await db.MergePublicAsync(true, [first]);
        var other = MergeRegressionHarness.Booked("other-1");
        other.DataSource = "OtherSource";
        other.Data = new JObject
        {
            ["Customer"] = new JObject { ["Name"] = "Maintained by another source" },
            ["lastUpdated"] = MergeRegressionHarness.SourceTime(1).AddSeconds(30)
        };
        if (includeKeys) other.Data["alternateKeys"] = new JArray(new JObject { ["Key"] = "customer.account", ["Value"] = "maintained-account" });
        var otherResponse = await db.MergePublicAsync(true, [other]);
        Assert.DoesNotContain(otherResponse.Results, r => MergeOutcomes.IsFailure(r.MergeOutcome));
        var rejected = MergeRegressionHarness.Booked(version: 2);
        rejected.Data["Customer"] = new JObject { ["Name"] = "Source name", ["Email"] = "later@example.test" };
        var later = MergeRegressionHarness.Booked(version: 3);
        later.Data["Customer"] = (JObject)rejected.Data["Customer"]!.DeepClone();
        if (includeKeys)
        {
            rejected.Data["alternateKeys"] = first.Data["alternateKeys"]!.DeepClone();
            later.Data["alternateKeys"] = first.Data["alternateKeys"]!.DeepClone();
        }
        var response = await db.MergePublicAsync(true, [rejected, later]);
        Assert.Single(response.Results, r => r.MergeOutcome == MergeOutcomes.MergeRejected);
        Assert.Equal("Maintained by another source", db.Stored["entity-1"].SelectToken("Customer.Name")?.Value<string>());
        Assert.Equal("later@example.test", db.Stored["entity-1"].SelectToken("Customer.Email")?.Value<string>());
        AssertBusinessEqual(db.Stored["entity-1"], ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1")));
        var source = ChangeTrackingHelper.ReassembleEntity(db.HistoryFor("TestSource", "Reservation", "booking-1"));
        Assert.Equal("Source name", source.SelectToken("Customer.Name")?.Value<string>());
        Assert.Equal("later@example.test", source.SelectToken("Customer.Email")?.Value<string>());
        if (includeKeys)
            Assert.Equal("maintained-account", db.Stored["entity-1"]["alternateKeys"]!.Single(k => k.Value<string>("Key") == "customer.account").Value<string>("Value"));
    }

    [Fact]
    public async Task Repeated_unchanged_inputs_do_not_add_history_or_write_the_projection()
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var first = MergeRegressionHarness.Booked();
        await db.MergePublicAsync(true, [first]);
        var sourceCount = db.HistoryFor("TestSource", "Reservation", "booking-1").Count;
        var destinationCount = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1").Count;
        var writes = db.EntityWriteCount("entity-1");
        var later = MergeRegressionHarness.Booked();
        later.Data["lastUpdated"] = MergeRegressionHarness.SourceTime(2);
        var latest = MergeRegressionHarness.Booked();
        latest.Data["lastUpdated"] = MergeRegressionHarness.SourceTime(3);
        var response = await db.MergePublicAsync(true, [later, latest]);
        Assert.Single(response.Results, r => r.MergeOutcome == MergeOutcomes.NoSourceEntityUpdateToProcess);
        Assert.Equal(sourceCount, db.HistoryFor("TestSource", "Reservation", "booking-1").Count);
        Assert.Equal(destinationCount, db.HistoryFor(DataSources.DataHub, "Booking", "entity-1").Count);
        Assert.Equal(writes, db.EntityWriteCount("entity-1"));
    }

    [Fact]
    public async Task A_metadata_failure_after_staging_discards_properties_and_keys_before_a_later_partial_update()
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var invalid = MergeRegressionHarness.Booked();
        invalid.Data["FailedOnly"] = "discard";
        invalid.Data["alternateKeys"] = new JArray(
            new JObject { ["Key"] = "customer.valid", ["Value"] = "staged" },
            new JObject { ["Key"] = "customer.invalid", ["Value"] = new JObject { ["not"] = "a string" } });
        var later = MergeRegressionHarness.Cancelled();
        later.Data = new JObject { ["Total"] = 0, ["lastUpdated"] = MergeRegressionHarness.SourceTime(2) };
        var response = await db.MergePublicAsync(true, [invalid, later]);
        Assert.Single(response.Results, r => r.MergeOutcome == MergeOutcomes.MergeFailed);
        Assert.Null(db.Stored["entity-1"]["FailedOnly"]);
        Assert.Equal(0, db.Stored["entity-1"].Value<int>("Total"));
        foreach (var history in Histories(db))
        {
            Assert.DoesNotContain(history, e => e.EntryType == ChangeTrackingEntryTypes.Update && e.Timestamp == MergeRegressionHarness.SourceTime(1));
            var replay = ChangeTrackingHelper.ReassembleEntity(history);
            Assert.Null(replay["FailedOnly"]);
            Assert.DoesNotContain(replay["alternateKeys"]!, k => k.Value<string>("Key") == "customer.valid");
            Assert.Equal(0, replay.Value<int>("Total"));
        }
    }

    [Fact]
    public async Task A_newer_partial_object_can_apply_after_its_earlier_object_was_filtered_by_freshness()
    {
        using var db = new MergeRegressionHarness(false) { Config = Rule("Customer", PropertyMergeRuleActions.OverwriteIfNewer) };
        db.SeedBooking("booking-1", "entity-1");
        db.Stored["entity-1"]["lastUpdated"] = MergeRegressionHarness.SourceTime(5);
        var stale = MergeRegressionHarness.Booked();
        stale.Data["Customer"] = new JObject { ["Name"] = "Old", ["Email"] = "source-only@example.test" };
        var newer = MergeRegressionHarness.Cancelled(version: 6);
        newer.Data["Customer"] = new JObject { ["Name"] = "New" };
        var response = await db.MergePublicAsync(true, [stale, newer]);
        Assert.DoesNotContain(response.Results, r => MergeOutcomes.IsFailure(r.MergeOutcome));
        Assert.Equal("New", db.Stored["entity-1"].SelectToken("Customer.Name")?.Value<string>());
        Assert.Null(db.Stored["entity-1"].SelectToken("Customer.Email"));
        var source = ChangeTrackingHelper.ReassembleEntity(db.HistoryFor("TestSource", "Reservation", "booking-1"));
        Assert.Equal("source-only@example.test", source.SelectToken("Customer.Email")?.Value<string>());
        AssertBusinessEqual(db.Stored["entity-1"], ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1")));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task A_later_input_repeating_a_rejected_value_still_applies_its_supplied_fields(bool silent, bool keysOnly)
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => check.ChangeSet?.Timestamp == MergeRegressionHarness.SourceTime(2)
                ? new() { Pass = false, Action = silent ? PreMergeRuleActions.SilentlyRejectMerge : PreMergeRuleActions.RejectMerge }
                : new() { Pass = true }
        };
        db.SeedBooking("booking-1", "entity-1");
        var first = MergeRegressionHarness.Booked();
        var rejected = MergeRegressionHarness.Cancelled();
        if (keysOnly)
        {
            first.Data = new JObject { ["lastUpdated"] = MergeRegressionHarness.SourceTime(1), ["alternateKeys"] = new JArray(new JObject { ["Key"] = "customer.account", ["Value"] = "first" }) };
            rejected.Data = new JObject { ["lastUpdated"] = MergeRegressionHarness.SourceTime(2), ["alternateKeys"] = new JArray(new JObject { ["Key"] = "customer.account", ["Value"] = "later" }) };
        }
        var repeated = new MergeEntityRequest
        {
            DataSource = rejected.DataSource, SourceEntityType = rejected.SourceEntityType,
            SourceEntityId = rejected.SourceEntityId, DataHubEntityType = rejected.DataHubEntityType,
            Data = (JObject)rejected.Data.DeepClone()
        };
        repeated.Data["lastUpdated"] = MergeRegressionHarness.SourceTime(3);
        var response = await db.MergePublicAsync(true, [first, rejected, repeated]);
        Assert.DoesNotContain(response.Results, r => r.MergeOutcome == MergeOutcomes.MergeFailed);
        var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
        var destination = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1");
        if (keysOnly)
        {
            foreach (var entity in new[] { db.Stored["entity-1"], ChangeTrackingHelper.ReassembleEntity(source), ChangeTrackingHelper.ReassembleEntity(destination) })
                Assert.Equal("later", entity["alternateKeys"]!.Single(k => k.Value<string>("Key") == "customer.account").Value<string>("Value"));
        }
        else
        {
            Assert.Equal(0, db.Stored["entity-1"].Value<int>("Total"));
            Assert.Equal(0, ChangeTrackingHelper.ReassembleEntity(source).Value<int>("Total"));
        }
        Assert.Contains(destination, e => e.EntryType == ChangeTrackingEntryTypes.Update && e.Timestamp == MergeRegressionHarness.SourceTime(3));
        Assert.DoesNotContain(destination, e => e.EntryType == ChangeTrackingEntryTypes.Update && e.Timestamp == MergeRegressionHarness.SourceTime(2));
        AssertBusinessEqual(db.Stored["entity-1"], ChangeTrackingHelper.ReassembleEntity(destination));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task Invalid_alternate_keys_discard_only_that_event_and_preserve_every_accepted_transition(int failedVersion, bool reverse)
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var baseline = (JObject)db.Stored["entity-1"].DeepClone();
        var inputs = Enumerable.Range(1, 3).Select(Version).ToList();
        inputs[failedVersion - 1].Data["alternateKeys"] = new JArray("malformed");
        if (reverse) inputs.Reverse();

        var response = await db.MergePublicAsync(true, inputs);
        Assert.True(response.Success, response.FailureReason);
        Assert.Single(response.Results, r => r.MergeOutcome == MergeOutcomes.MergeFailed);
        var accepted = inputs.Where(r => r.Data.Value<DateTimeOffset>("lastUpdated") != MergeRegressionHarness.SourceTime(failedVersion))
            .OrderBy(r => r.Data.Value<DateTimeOffset>("lastUpdated")).ToList();
        AssertAcceptedHistory(db, baseline, accepted);
        Assert.Null(db.Stored["entity-1"][$"OnlyVersion{failedVersion}"]);
        Assert.Equal(1, db.EntityWriteCount("entity-1"));
        Assert.Single(db.MergeFailures);
        Assert.Single(db.Notifications.OfType<DataHubEntityUpdatedNotification>());
        Assert.Equal(MergeRegressionHarness.ProcessingTime, db.Stored["entity-1"].DataHubLastUpdated());
    }

    [Fact]
    public async Task A_single_invalid_event_produces_no_projection_history_success_log_or_update_notification()
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var baseline = (JObject)db.Stored["entity-1"].DeepClone();
        var input = Version(1);
        input.Data["alternateKeys"] = new JArray(new JObject { ["Key"] = "customer.account", ["Value"] = "valid-before-error" }, "malformed");
        var response = await db.MergePublicAsync(true, [input]);

        Assert.Single(response.Results);
        Assert.Equal(MergeOutcomes.MergeFailed, response.Results.Single().MergeOutcome);
        AssertJson(baseline, db.Stored["entity-1"]);
        Assert.Equal(0, db.EntityWriteCount("entity-1"));
        Assert.Single(db.HistoryFor("TestSource", "Reservation", "booking-1"));
        Assert.Single(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1"));
        Assert.Empty(db.MergeSuccesses);
        Assert.Single(db.MergeFailures);
        Assert.Empty(db.Notifications.OfType<DataHubEntityUpdatedNotification>());
    }

    [Fact]
    public async Task All_invalid_versions_leave_both_histories_and_the_projection_unchanged()
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var baseline = (JObject)db.Stored["entity-1"].DeepClone();
        var inputs = Enumerable.Range(1, 3).Select(Version).ToList();
        foreach (var input in inputs) input.Data["alternateKeys"] = new JArray("malformed");
        var response = await db.MergePublicAsync(true, inputs);
        Assert.Equal(3, response.Results.Count(r => r.MergeOutcome == MergeOutcomes.MergeFailed));
        AssertJson(baseline, db.Stored["entity-1"]);
        Assert.Single(db.HistoryFor("TestSource", "Reservation", "booking-1"));
        Assert.Single(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1"));
        Assert.Equal(0, db.EntityWriteCount("entity-1"));
        Assert.Empty(db.MergeSuccesses);
    }

    [Fact]
    public async Task A_later_return_to_the_initial_value_is_not_lost_after_a_failed_event()
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var invalid = MergeRegressionHarness.Booked();
        invalid.Data["alternateKeys"] = new JArray("malformed");
        await db.MergePublicAsync(true, [invalid]);
        var later = MergeRegressionHarness.Cancelled();
        later.Data["Total"] = 10;
        var response = await db.MergePublicAsync(true, [later]);
        Assert.True(response.Success, response.FailureReason);
        Assert.DoesNotContain(response.Results, r => MergeOutcomes.IsFailure(r.MergeOutcome));
        Assert.Equal(10, db.Stored["entity-1"].Value<int>("Total"));
        foreach (var history in Histories(db))
        {
            Assert.Equal(10, ChangeTrackingHelper.ReassembleEntity(history).Value<int>("Total"));
            Assert.DoesNotContain(history, e => e.EntryType == ChangeTrackingEntryTypes.Update && e.Timestamp == MergeRegressionHarness.SourceTime(1));
            Assert.Contains(history, e => e.EntryType == ChangeTrackingEntryTypes.Update && e.Timestamp == MergeRegressionHarness.SourceTime(2));
        }
    }

    [Fact]
    public async Task Retrying_a_failed_event_once_restores_it_and_repeating_the_success_does_not_duplicate_source_history()
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var baseline = (JObject)db.Stored["entity-1"].DeepClone();
        var valid = Version(1);
        var invalid = Version(1);
        invalid.Data["alternateKeys"] = new JArray("malformed");
        await db.MergePublicAsync(true, [invalid]);
        var retry = await db.MergePublicAsync(true, [valid]);
        Assert.DoesNotContain(retry.Results, r => MergeOutcomes.IsFailure(r.MergeOutcome));
        AssertAcceptedHistory(db, baseline, [valid]);
        var before = db.HistoryFor("TestSource", "Reservation", "booking-1").Count;
        await db.MergePublicAsync(true, [valid]);
        Assert.Equal(before, db.HistoryFor("TestSource", "Reservation", "booking-1").Count);
    }

    [Fact]
    public async Task An_invalid_source_event_does_not_discard_an_independent_source_update()
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        db.SeedBooking("booking-2", "entity-2");
        var invalid = Version(1);
        invalid.Data["alternateKeys"] = new JArray("malformed");
        var valid = MergeRegressionHarness.Booked("booking-2");
        var response = await db.MergePublicAsync(true, [invalid, valid]);
        Assert.Contains(response.Results, r => r.SourceEntityId == "booking-1" && r.MergeOutcome == MergeOutcomes.MergeFailed);
        Assert.Contains(response.Results, r => r.SourceEntityId == "booking-2" && r.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated);
        Assert.Equal(10, db.Stored["entity-1"].Value<int>("Total"));
        Assert.Equal(42, db.Stored["entity-2"].Value<int>("Total"));
        Assert.Single(db.HistoryFor("TestSource", "Reservation", "booking-1"));
        Assert.Equal(2, db.HistoryFor("TestSource", "Reservation", "booking-2").Count);
        Assert.Equal(42, ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(DataSources.DataHub, "Booking", "entity-2")).Value<int>("Total"));
        Assert.DoesNotContain(db.MergeSuccesses, r => r.SourceEntityId == "booking-1");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Different_sources_sharing_a_destination_are_explicitly_rejected_without_losing_independent_updates(bool tracked, bool reverse)
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        db.SeedSourceAlias("booking-2", "entity-1");
        db.SeedBooking("booking-3", "entity-3");
        var baseline = (JObject)db.Stored["entity-1"].DeepClone();
        var inputs = new List<MergeEntityRequest> { MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled("booking-2"), MergeRegressionHarness.Booked("booking-3") };
        if (reverse) inputs.Reverse();
        var response = await db.MergePublicAsync(tracked, inputs);
        Assert.True(response.Success, response.FailureReason);
        foreach (var sourceId in new[] { "booking-1", "booking-2" })
        {
            var result = Assert.Single(response.Results, r => r.SourceEntityId == sourceId);
            Assert.Equal(MergeOutcomes.MergeFailed, result.MergeOutcome);
            Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
            Assert.DoesNotContain(db.MergeSuccesses, r => r.SourceEntityId == sourceId);
            if (tracked) Assert.Single(db.MergeFailures, r => r.SourceEntityId == sourceId);
            Assert.Single(db.HistoryFor("TestSource", "Reservation", sourceId));
        }
        AssertJson(baseline, db.Stored["entity-1"]);
        Assert.Single(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1"));
        Assert.Equal(0, db.EntityWriteCount("entity-1"));
        Assert.Contains(response.Results, r => r.SourceEntityId == "booking-3" && r.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated);
        Assert.Equal(42, db.Stored["entity-3"].Value<int>("Total"));
        Assert.Equal(tracked ? 2 : 1, db.HistoryFor("TestSource", "Reservation", "booking-3").Count);
        Assert.DoesNotContain(db.Notifications.OfType<DataHubEntityUpdatedNotification>(), n => n.DataHubEntityId == "entity-1");
        if (!tracked) Assert.Empty(db.MergeFailures);
    }

    [Theory]
    [InlineData(true, null, 0)]
    [InlineData(false, null, 0)]
    [InlineData(true, PropertyMergeRuleActions.AlwaysOverwrite, 0)]
    [InlineData(false, PropertyMergeRuleActions.AlwaysOverwrite, 0)]
    [InlineData(true, PropertyMergeRuleActions.NeverOverwrite, 10)]
    [InlineData(false, PropertyMergeRuleActions.NeverOverwrite, 10)]
    public async Task Valid_public_batches_preserve_partial_fields_arrays_objects_and_source_observations(bool tracked, string? action, int expectedTotal)
    {
        using var db = new MergeRegressionHarness(false) { Config = action == null ? null : Rule("Total", action) };
        db.SeedBooking("booking-1", "entity-1");
        var inputs = Enumerable.Range(1, 3).Select(v => v == 3 ? MergeRegressionHarness.Cancelled(version: v) : MergeRegressionHarness.Booked(version: v)).ToList();
        inputs[0].Data["DeliveryNote"] = "Keep this source update";
        inputs[0].Data["Customer"] = new JObject { ["Name"] = "First", ["Contact"] = new JObject { ["Email"] = "first@example.test" } };
        inputs[1].Data["Customer"] = new JObject { ["Name"] = "Second", ["Contact"] = new JObject { ["Email"] = null } };
        inputs.Reverse();
        var response = await db.MergePublicAsync(tracked, inputs);
        Assert.True(response.Success, response.FailureReason);
        Assert.DoesNotContain(response.Results, r => MergeOutcomes.IsFailure(r.MergeOutcome));
        Assert.Equal(expectedTotal, db.Stored["entity-1"].Value<int>("Total"));
        Assert.Equal("Keep this source update", db.Stored["entity-1"].Value<string>("DeliveryNote"));
        Assert.Equal("Second", db.Stored["entity-1"].SelectToken("Customer.Name")!.Value<string>());
        Assert.True(db.Stored["entity-1"].SelectToken("Customer.Contact.Email") == null || db.Stored["entity-1"].SelectToken("Customer.Contact.Email")!.Type == JTokenType.Null);
        Assert.Equal("Cancelled", db.Stored["entity-1"].SelectToken("BookingLines[0].Status")!.Value<string>());
        Assert.Equal(1, db.EntityWriteCount("entity-1"));
        Assert.Equal(MergeRegressionHarness.ProcessingTime, db.Stored["entity-1"].DataHubLastUpdated());
        if (tracked)
        {
            var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
            Assert.Equal(new[] { 1, 2, 3 }.Select(MergeRegressionHarness.SourceTime), source.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp));
            Assert.Equal(0, ChangeTrackingHelper.ReassembleEntity(source).Value<int>("Total"));
            AssertBusinessEqual(db.Stored["entity-1"], ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1")));
        }
        else
        {
            Assert.Single(db.HistoryFor("TestSource", "Reservation", "booking-1"));
            Assert.Single(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1"));
        }
    }

    [Theory]
    [InlineData(PropertyMergeRuleActions.NeverOverwrite)]
    [InlineData(PropertyMergeRuleActions.DoNotUpdate)]
    [InlineData(PropertyMergeRuleActions.OverwriteIfNewer)]
    public async Task Property_rules_filter_destination_values_without_losing_source_system_updates(string action)
    {
        using var db = new MergeRegressionHarness(false) { Config = Rule("Total", action) };
        db.SeedBooking("booking-1", "entity-1");
        if (action == PropertyMergeRuleActions.OverwriteIfNewer) db.Stored["entity-1"]["lastUpdated"] = MergeRegressionHarness.SourceTime(5);
        var inputs = new List<MergeEntityRequest> { MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled() };
        var response = await db.MergePublicAsync(true, inputs);
        Assert.DoesNotContain(response.Results, r => MergeOutcomes.IsFailure(r.MergeOutcome));
        var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
        Assert.Equal(new[] { MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(2) }, source.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp));
        Assert.Equal(42, ChangeTrackingHelper.ReassembleEntity(source.Where(e => e.Timestamp <= MergeRegressionHarness.SourceTime(1)).ToList()).Value<int>("Total"));
        Assert.Equal(0, ChangeTrackingHelper.ReassembleEntity(source).Value<int>("Total"));
        Assert.Equal(10, db.Stored["entity-1"].Value<int>("Total"));
        AssertBusinessEqual(db.Stored["entity-1"], ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1")));
    }

    [Theory]
    [InlineData("hard")]
    [InlineData("silent")]
    [InlineData("exception")]
    public async Task Pre_merge_outcomes_preserve_the_expected_source_observations_and_accepted_neighbours(string outcome)
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => check.ChangeSet?.Timestamp != MergeRegressionHarness.SourceTime(2)
                ? new() { Pass = true }
                : outcome == "exception" ? throw new InvalidOperationException("Injected pre-merge failure")
                : new() { Pass = false, Action = outcome == "silent" ? PreMergeRuleActions.SilentlyRejectMerge : PreMergeRuleActions.RejectMerge }
        };
        db.SeedBooking("booking-1", "entity-1");
        var baseline = (JObject)db.Stored["entity-1"].DeepClone();
        var inputs = Enumerable.Range(1, 3).Select(Version).ToList();
        // Alternate keys are covered separately; pre-merge rejection must not hide
        // business source observations even when the destination rejects the event.
        foreach (var input in inputs) input.Data.Remove("alternateKeys");
        await db.MergePublicAsync(true, inputs);
        var sourceVersions = outcome == "silent" ? new[] { 1, 2, 3 } : new[] { 1, 3 };
        var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
        Assert.Equal(sourceVersions.Select(MergeRegressionHarness.SourceTime), source.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp));
        AssertBusinessEqual(Expected(baseline, inputs.Where(i => sourceVersions.Contains(i.Data.Value<DateTimeOffset>("lastUpdated").Minute))), ChangeTrackingHelper.ReassembleEntity(source));
        AssertBusinessEqual(Expected(baseline, [inputs[0], inputs[2]]), db.Stored["entity-1"]);
        AssertBusinessEqual(db.Stored["entity-1"], ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_failed_projection_does_not_hide_an_independent_success_or_advance_its_failed_source(bool tracked)
    {
        using var db = new MergeRegressionHarness(false) { FailEntity = "entity-1" };
        db.SeedBooking("booking-1", "entity-1");
        db.SeedBooking("booking-2", "entity-2");
        var response = await db.MergePublicAsync(tracked, [MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled(), MergeRegressionHarness.Booked("booking-2")]);
        Assert.Contains(response.Results, r => r.SourceEntityId == "booking-1" && r.MergeOutcome == MergeOutcomes.MergeFailed);
        Assert.Contains(response.Results, r => r.SourceEntityId == "booking-2" && r.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated);
        Assert.Single(db.HistoryFor("TestSource", "Reservation", "booking-1"));
        Assert.Equal(tracked ? 2 : 1, db.HistoryFor("TestSource", "Reservation", "booking-2").Count);
        Assert.DoesNotContain(db.MergeSuccesses, r => r.SourceEntityId == "booking-1");
        Assert.Equal(42, db.Stored["entity-2"].Value<int>("Total"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Alternate_key_only_updates_preserve_business_data_and_can_skip_one_invalid_version(bool invalidMiddle)
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        var inputs = Enumerable.Range(1, 3).Select(version => new MergeEntityRequest
        {
            DataSource = "TestSource", SourceEntityType = "Reservation", SourceEntityId = "booking-1", DataHubEntityType = "Booking",
            Data = new JObject { ["lastUpdated"] = MergeRegressionHarness.SourceTime(version), ["alternateKeys"] = JArray.FromObject(new[] { new AlternateKey("customer.account", $"account-{version}") }) }
        }).ToList();
        if (invalidMiddle) inputs[1].Data["alternateKeys"] = new JArray("malformed");
        var response = await db.MergePublicAsync(true, inputs.AsEnumerable().Reverse().ToList());
        Assert.Equal(invalidMiddle ? 1 : 0, response.Results.Count(r => MergeOutcomes.IsFailure(r.MergeOutcome)));
        var expectedVersions = invalidMiddle ? new[] { 1, 3 } : new[] { 1, 2, 3 };
        foreach (var history in Histories(db))
        {
            Assert.Equal(expectedVersions.Select(MergeRegressionHarness.SourceTime), history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp));
            foreach (var version in expectedVersions)
            {
                var replay = ChangeTrackingHelper.ReassembleEntity(history.Where(e => e.Timestamp <= MergeRegressionHarness.SourceTime(version)).ToList());
                Assert.Equal(10, replay.Value<int>("Total"));
                Assert.Equal($"account-{version}", replay["alternateKeys"]!.ToObject<List<AlternateKey>>()!.Single(k => k.Key == "customer.account").Value);
            }
        }
        Assert.Equal(10, db.Stored["entity-1"].Value<int>("Total"));
        Assert.Equal("account-3", db.Stored["entity-1"]["alternateKeys"]!.ToObject<List<AlternateKey>>()!.Single(k => k.Key == "customer.account").Value);
    }

    [Fact]
    public async Task Partial_update_after_a_rejected_parent_reset_preserves_the_earlier_accepted_sibling_field()
    {
        using var db = new MergeRegressionHarness(false)
        {
            Config = new EntityConfig(),
            PreMergeRule = check => new CheckPreMergeRulesResponse { Pass = check.ChangeSet?.Timestamp != MergeRegressionHarness.SourceTime(2), Action = PreMergeRuleActions.RejectMerge }
        };
        db.SeedBooking("booking-1", "entity-1");
        var first = MergeRegressionHarness.Booked();
        first.Data["Customer"] = new JObject { ["Name"] = "First", ["Email"] = "must-survive@example.test" };
        var rejected = MergeRegressionHarness.Booked(version: 2);
        rejected.Data["Customer"] = null;
        var later = MergeRegressionHarness.Booked(version: 3);
        later.Data["Customer"] = new JObject { ["Name"] = "Later" };
        var response = await db.MergePublicAsync(true, [first, rejected, later]);
        Assert.Single(response.Results, r => r.MergeOutcome == MergeOutcomes.MergeRejected);
        foreach (var entity in Histories(db).Select(history => ChangeTrackingHelper.ReassembleEntity(history)).Append(db.Stored["entity-1"]))
        {
            Assert.Equal("Later", entity.SelectToken("Customer.Name")?.Value<string>());
            Assert.Equal("must-survive@example.test", entity.SelectToken("Customer.Email")?.Value<string>());
        }
    }

    [Theory]
    [InlineData(PropertyMergeRuleActions.DoNotUpdate)]
    [InlineData(PropertyMergeRuleActions.NeverOverwrite)]
    public async Task Alternate_key_rules_preserve_source_key_observations_without_changing_destination_keys(string action)
    {
        using var db = new MergeRegressionHarness(false) { Config = Rule("alternateKeys", action) };
        db.SeedBooking("booking-1", "entity-1");
        var response = await db.MergePublicAsync(true, [Version(1), Version(2)]);
        Assert.DoesNotContain(response.Results, r => MergeOutcomes.IsFailure(r.MergeOutcome));
        var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
        Assert.Equal(new[] { MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(2) }, source.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp));
        Assert.Equal("account-2", ChangeTrackingHelper.ReassembleEntity(source)["alternateKeys"]!.ToObject<List<AlternateKey>>()!.Single(k => k.Key == "customer.account").Value);
        Assert.DoesNotContain(db.Stored["entity-1"]["alternateKeys"]!.ToObject<List<AlternateKey>>()!, k => k.Key == "customer.account");
        AssertBusinessEqual(db.Stored["entity-1"], ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1")));
    }

    [Fact]
    public async Task Filtering_every_destination_property_still_preserves_the_source_observation()
    {
        using var db = new MergeRegressionHarness(false) { Config = Rule("*", PropertyMergeRuleActions.NeverOverwrite) };
        db.SeedBooking("booking-1", "entity-1");
        var response = await db.MergePublicAsync(true, [MergeRegressionHarness.Booked(), MergeRegressionHarness.Cancelled()]);
        Assert.DoesNotContain(response.Results, r => MergeOutcomes.IsFailure(r.MergeOutcome));
        var source = db.HistoryFor("TestSource", "Reservation", "booking-1");
        Assert.Equal(new[] { MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(2) }, source.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp));
        Assert.Equal(0, ChangeTrackingHelper.ReassembleEntity(source).Value<int>("Total"));
        Assert.Equal(10, db.Stored["entity-1"].Value<int>("Total"));
        Assert.Single(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1"));
        Assert.Equal(0, db.EntityWriteCount("entity-1"));
    }

    [Theory]
    [InlineData(true, "OtherSource", "Reservation")]
    [InlineData(false, "OtherSource", "Reservation")]
    [InlineData(true, "TestSource", "OtherType")]
    [InlineData(false, "TestSource", "OtherType")]
    public async Task Separate_source_system_or_type_groups_can_continue_to_update_the_same_destination(bool tracked, string dataSource, string sourceType)
    {
        using var db = new MergeRegressionHarness(false);
        db.SeedBooking("booking-1", "entity-1");
        db.SeedSourceAlias("booking-2", "entity-1", dataSource, sourceType);
        var second = MergeRegressionHarness.Cancelled("booking-2");
        second.DataSource = dataSource;
        second.SourceEntityType = sourceType;
        var response = await db.MergePublicAsync(tracked, [MergeRegressionHarness.Booked(), second]);
        Assert.True(response.Success, response.FailureReason);
        Assert.Equal(2, response.Results.Count(r => r.MergeOutcome == MergeOutcomes.EntityMatchedAndUpdated));
        Assert.DoesNotContain(response.Results, r => MergeOutcomes.IsFailure(r.MergeOutcome));
        Assert.Equal(0, db.Stored["entity-1"].Value<int>("Total"));
        Assert.Contains(db.Notifications.OfType<DataHubEntityUpdatedNotification>(), n => n.SourceEntityId == "booking-1");
        Assert.Contains(db.Notifications.OfType<DataHubEntityUpdatedNotification>(), n => n.SourceEntityId == "booking-2");
        if (tracked)
        {
            Assert.Contains(db.MergeSuccesses, r => r.SourceEntityId == "booking-1");
            Assert.Contains(db.MergeSuccesses, r => r.SourceEntityId == "booking-2");
            Assert.Equal(MergeRegressionHarness.SourceTime(1), Assert.Single(db.HistoryFor("TestSource", "Reservation", "booking-1"), e => e.EntryType == ChangeTrackingEntryTypes.Update).Timestamp);
            Assert.Equal(MergeRegressionHarness.SourceTime(2), Assert.Single(db.HistoryFor(dataSource, sourceType, "booking-2"), e => e.EntryType == ChangeTrackingEntryTypes.Update).Timestamp);
            AssertBusinessEqual(db.Stored["entity-1"], ChangeTrackingHelper.ReassembleEntity(db.HistoryFor(DataSources.DataHub, "Booking", "entity-1")));
        }
    }

    private static MergeEntityRequest Version(int version)
    {
        var input = MergeRegressionHarness.Booked(version: version);
        input.Data["Total"] = version * 10;
        input.Data[$"OnlyVersion{version}"] = $"source update {version}";
        input.Data["Customer"] = new JObject { ["Name"] = $"Version {version}", ["Tags"] = new JArray(version, version + 1) };
        input.Data["alternateKeys"] = JArray.FromObject(new[] { new AlternateKey("customer.account", $"account-{version}") });
        return input;
    }

    private static EntityConfig Rule(string property, string action) => new()
    {
        MergeRules = [new MergeRule { DataSource = "*", SourceEntityType = "*", Context = "*", Rules = [new PropertyMergeRule { PropertyName = property, Action = action }] }]
    };

    private static IEnumerable<List<ChangeTrackingEntry>> Histories(MergeRegressionHarness db)
    {
        yield return db.HistoryFor("TestSource", "Reservation", "booking-1");
        yield return db.HistoryFor(DataSources.DataHub, "Booking", "entity-1");
    }

    private static void AssertAcceptedHistory(MergeRegressionHarness db, JObject baseline, List<MergeEntityRequest> accepted)
    {
        var expectedTimes = accepted.Select(i => i.Data.Value<DateTimeOffset>("lastUpdated")).ToArray();
        Assert.Equal(expectedTimes, db.HistoryFor("TestSource", "Reservation", "booking-1")
            .Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp));
        foreach (var history in Histories(db))
        {
            Assert.Equal(expectedTimes, history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).Select(e => e.Timestamp).Distinct().Order());
            foreach (var time in Enumerable.Range(0, 4).Select(MergeRegressionHarness.SourceTime))
            {
                var acceptedAtTime = accepted.Where(i => i.Data.Value<DateTimeOffset>("lastUpdated") <= time).ToList();
                var expected = Expected(baseline, acceptedAtTime);
                var replay = ChangeTrackingHelper.ReassembleEntity(history.Where(e => e.Timestamp <= time).ToList());
                AssertBusinessEqual(expected, replay);
                var account = replay["alternateKeys"]!.ToObject<List<AlternateKey>>()!.SingleOrDefault(k => k.Key == "customer.account");
                if (acceptedAtTime.Count == 0) Assert.Null(account);
                else Assert.Equal(acceptedAtTime.Last().Data["alternateKeys"]![0]!.Value<string>("Value"), account?.Value);
            }
        }
        AssertBusinessEqual(Expected(baseline, accepted), db.Stored["entity-1"]);
        var lastKey = accepted.Last().Data["alternateKeys"]![0]!.Value<string>("Value");
        Assert.Equal(lastKey, db.Stored["entity-1"]["alternateKeys"]!.ToObject<List<AlternateKey>>()!.Single(k => k.Key == "customer.account").Value);
    }

    // Independent snapshot oracle: apply accepted input fields, not production deltas.
    private static JObject Expected(JObject baseline, IEnumerable<MergeEntityRequest> accepted)
    {
        var result = (JObject)baseline.DeepClone();
        foreach (var input in accepted) result.Merge(input.Data, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace, MergeNullValueHandling = MergeNullValueHandling.Merge });
        return result;
    }

    private static void AssertBusinessEqual(JObject expected, JObject actual)
    {
        static JObject Business(JObject value)
        {
            var result = (JObject)value.DeepClone();
            foreach (var name in new[] { "id", "entityType", "lastUpdated", "createdOn", "alternateKeys", "_ts", "_etag", "_dnf", "_dt", "pk" }) result.Remove(name);
            return result.RemoveNullValues();
        }
        AssertJson(Business(expected), Business(actual));
    }

    private static void AssertJson(JObject expected, JObject actual) =>
        Assert.True(JToken.DeepEquals(Normalize(expected), Normalize(actual)), $"Expected: {expected}\nActual: {actual}");

    // Persistence/replay can change JSON numeric types and ISO timestamp formatting.
    // Compare their values while still detecting missing properties and array order.
    private static JToken Normalize(JToken value) => value switch
    {
        JObject obj => new JObject(obj.Properties().OrderBy(p => p.Name).Select(p => new JProperty(p.Name, Normalize(p.Value)))),
        JArray array => new JArray(array.Select(Normalize)),
        { Type: JTokenType.Integer or JTokenType.Float } => new JValue(value.Value<decimal>()),
        { Type: JTokenType.Date } => new JValue(value.AsDateTimeOffset()!.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
        { Type: JTokenType.String } when value.Value<string>()!.Contains('T') && DateTimeOffset.TryParse(value.Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            => new JValue(date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
        _ => value.DeepClone()
    };
}
