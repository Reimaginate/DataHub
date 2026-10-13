using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.DataHub.SharedModels.Rules;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

// OverwriteIfNewer uses the stored document timestamp as an agreed freshness proxy.
// Saving still stamps processing time for incremental sync; history keeps source times.
[Trait("Category", "MergeRegression")]
public class PropertyMergeFreshnessRegressionTests
{
    [Theory]
    [InlineData(false, -1, 10)]
    [InlineData(false, 0, 42)]
    [InlineData(false, 1, 42)]
    [InlineData(true, -1, 10)]
    [InlineData(true, 0, 42)]
    [InlineData(true, 1, 42)]
    public async Task OverwriteIfNewer_uses_document_timestamp_and_allows_equal_times(bool tracked, int incomingMinutes, int expectedTotal)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: false);
        db.SeedBooking("booking-1", "entity-1");
        var baseline = db.Stored["entity-1"];
        baseline["lastUpdated"] = MergeRegressionHarness.SourceTime(2);
        var incoming = MergeRegressionHarness.Booked();
        incoming.Data["lastUpdated"] = MergeRegressionHarness.SourceTime(2).AddMinutes(incomingMinutes);
        db.Config = FreshnessRule();

        var results = await db.MergeUpdatesAsync(tracked, [incoming]);

        results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(expectedTotal);
        db.Stored["entity-1"].DataHubLastUpdated().Should().Be(MergeRegressionHarness.ProcessingTime);
        db.EntityWriteCount("entity-1").Should().Be(1);
        if (tracked)
        {
            var change = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1")
                .Single(e => e.EntryType == ChangeTrackingEntryTypes.Update);
            change.Timestamp.Should().Be(incoming.Data.DataHubLastUpdated());
            change.Data.ContainsKey("Total").Should().Be(incomingMinutes >= 0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_source_change_uses_the_saved_sync_timestamp_as_the_proxy(bool tracked)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: true) { Config = FreshnessRule() };
        db.SeedBooking("booking-1", "entity-1");

        var first = await db.MergePublicAsync(tracked, [MergeRegressionHarness.Booked(version: 10)]);
        var second = await db.MergePublicAsync(tracked, [MergeRegressionHarness.Cancelled(version: 20)]);

        first.Success.Should().BeTrue(first.FailureReason);
        second.Success.Should().BeTrue(second.FailureReason);
        // The second source event is later than the first, but earlier than the saved
        // processing time. The agreed document proxy deliberately preserves Total.
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(42);
        db.Stored["entity-1"].SelectToken("BookingLines[0].Status")!.Value<string>().Should().Be("Cancelled",
            "a freshness rule on Total must not reject unrelated properties");
        db.Stored["entity-1"].DataHubLastUpdated().Should().Be(MergeRegressionHarness.ProcessingTime);
        db.Notifications.OfType<DataHubEntityUpdatedNotification>().Should().HaveCount(2)
            .And.OnlyContain(n => n.SourceEventTimeStamp == MergeRegressionHarness.ProcessingTime);
        if (tracked)
        {
            var sourceUpdates = db.HistoryFor("TestSource", "Reservation", "booking-1")
                .Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).ToList();
            sourceUpdates.Select(e => e.Timestamp).Should().Equal(MergeRegressionHarness.SourceTime(10), MergeRegressionHarness.SourceTime(20));
            var dataHubUpdates = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1")
                .Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).ToList();
            dataHubUpdates.Select(e => e.Timestamp).Should().Equal(MergeRegressionHarness.SourceTime(10), MergeRegressionHarness.SourceTime(20));
            dataHubUpdates[1].Data.Should().NotContainKey("Total");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordered_batch_keeps_accepted_property_changes_and_individual_history(bool tracked)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: true) { Config = FreshnessRule() };
        db.SeedBooking("booking-1", "entity-1");
        db.Stored["entity-1"]["lastUpdated"] = MergeRegressionHarness.SourceTime(2);
        var equal = MergeRegressionHarness.Booked(version: 2);
        equal.Data["Total"] = 21m;

        var response = await db.MergePublicAsync(tracked,
            [MergeRegressionHarness.Booked(), equal, MergeRegressionHarness.Cancelled(version: 3)]);

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(0);
        db.Stored["entity-1"].DataHubLastUpdated().Should().Be(MergeRegressionHarness.ProcessingTime);
        db.EntityWriteCount("entity-1").Should().Be(1);
        if (tracked)
        {
            var history = db.HistoryFor(DataSources.DataHub, "Booking", "entity-1");
            var updates = history.Where(e => e.EntryType == ChangeTrackingEntryTypes.Update).ToList();
            updates.Select(e => e.Timestamp).Should().Equal(MergeRegressionHarness.SourceTime(1), MergeRegressionHarness.SourceTime(2), MergeRegressionHarness.SourceTime(3));
            updates[0].Data.Should().NotContainKey("Total");
            updates[1].Data["Total"]![0]!.Value<decimal>().Should().Be(10,
                "history must describe the transition from the accepted value, not the rejected source value");
            updates[1].Data["Total"]![1]!.Value<decimal>().Should().Be(21);
            ChangeTrackingHelper.ReassembleEntity(history.Where(e => e.Timestamp <= MergeRegressionHarness.SourceTime(1)).ToList())
                .Value<decimal>("Total").Should().Be(10);
            ChangeTrackingHelper.ReassembleEntity(history.Where(e => e.Timestamp <= MergeRegressionHarness.SourceTime(2)).ToList())
                .Value<decimal>("Total").Should().Be(21);
            ChangeTrackingHelper.ReassembleEntity(history).Value<decimal>("Total").Should().Be(0);
        }
    }

    [Theory]
    [InlineData(-1, 10, "Pending")]
    [InlineData(1, 42, "Booked")]
    public async Task Untracked_wildcard_freshness_rule_checks_nested_values_before_stripping_metadata(int incomingMinutes, int expectedTotal, string expectedStatus)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: false) { Config = FreshnessRule("*") };
        db.SeedBooking("booking-1", "entity-1");
        db.Stored["entity-1"]["lastUpdated"] = MergeRegressionHarness.SourceTime(2);
        var incoming = MergeRegressionHarness.Booked();
        incoming.Data["lastUpdated"] = MergeRegressionHarness.SourceTime(2).AddMinutes(incomingMinutes);

        var results = await db.MergeUpdatesAsync(tracked: false, [incoming]);

        results.Should().NotContain(r => MergeOutcomes.IsFailure(r.MergeOutcome));
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(expectedTotal);
        db.Stored["entity-1"].SelectToken("BookingLines[0].Status")!.Value<string>().Should().Be(expectedStatus);
        db.Stored["entity-1"].DataHubLastUpdated().Should().Be(MergeRegressionHarness.ProcessingTime);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "not-a-timestamp")]
    [InlineData(false, "not-a-timestamp")]
    public async Task Untracked_freshness_rule_reports_unusable_timestamps_without_overwriting(bool invalidIncoming, string? value)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: false) { Config = FreshnessRule() };
        db.SeedBooking("booking-1", "entity-1");
        var incoming = MergeRegressionHarness.Booked();
        var timestampOwner = invalidIncoming ? incoming.Data : db.Stored["entity-1"];
        if (value == null) timestampOwner.Remove("lastUpdated");
        else timestampOwner["lastUpdated"] = value;

        var results = await db.MergeUpdatesAsync(tracked: false, [incoming]);

        results.Should().ContainSingle(r => r.MergeOutcome == MergeOutcomes.MergeFailed && !string.IsNullOrEmpty(r.FailureReason));
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(10);
        db.EntityWriteCount("entity-1").Should().Be(0);
    }

    private static EntityConfig FreshnessRule(string property = "Total") => new()
    {
        MergeRules = [new MergeRule { DataSource = "*", SourceEntityType = "*", Context = "*",
            Rules = [new PropertyMergeRule { PropertyName = property, Action = PropertyMergeRuleActions.OverwriteIfNewer }] }]
    };
}
