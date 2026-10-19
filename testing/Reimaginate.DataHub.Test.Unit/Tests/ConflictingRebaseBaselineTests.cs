using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Xunit;
using Harness = Reimaginate.DataHub.Test.Unit.Tests.RebaseTrackingPersistenceTests.Harness;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class ConflictingRebaseBaselineTests
{
    [Theory]
    [InlineData("internal", false, false, true)]
    [InlineData("client", true, true, true)]
    [InlineData("cli", false, true, false)]
    [InlineData("internal", true, false, false)]
    public async Task Conflicts_use_the_stored_snapshot_even_when_later_or_same_timestamp_updates_replay_successfully(
        string surface, bool sameTimestamp, bool reverse, bool include)
    {
        var stored = Stored();
        var storedJson = stored.ToString();
        var db = Conflict(false, stored);
        if (sameTimestamp) db.History.Single(entry => entry.EntryType == "Update").Timestamp = Harness.Start;
        if (reverse) db.History.Reverse();
        var originals = db.History.ToList();

        var result = (await db.Rebase(surface: surface, includeArchivedTrackingEntries: include)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().Contain("STORED_ENTITY_RECOVERY");
        result.RebasedTo.Should().Be(sameTimestamp ? Harness.Start : Harness.Start.AddMinutes(1));
        result.ArchivedTrackingEntryCount.Should().Be(3);
        if (include) result.ArchivedTrackingEntries.Should().BeEquivalentTo(originals);
        else result.ArchivedTrackingEntries.Should().BeEmpty();
        db.MaterializeCalls.Should().Be(0, "neither conflicting baseline may be chosen for reconstruction");
        db.History.Should().ContainSingle();
        JToken.DeepEquals(db.History[0].Data, RebaseTrackingPersistence.CreateBaselineData(stored)).Should().BeTrue();
        db.MaterializedWrites.Should().BeEmpty();
        stored.ToString().Should().Be(storedJson);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Source_preserves_conflicts_and_the_entire_tail_and_compacts_only_an_earlier_valid_prefix(bool prefix, bool reverse)
    {
        var db = new Harness(true);
        var conflictAt = prefix ? Harness.Start.AddMinutes(2) : Harness.Start;
        if (prefix)
            db.History.Add(db.Entry("conflict-a", "entity-1", "Init", conflictAt, new JObject { ["Value"] = 100 }));
        db.History.Add(db.Entry("conflict-b", "entity-1", "Init", conflictAt, new JObject { ["Value"] = 200 }));
        db.History.Add(db.Entry("same-time-update", "entity-1", "Update", conflictAt, JObject.Parse("{ 'Status': ['ambiguous'] }")));
        // Even a later single baseline cannot make it safe to discard the conflicting group.
        db.History.Add(db.Entry("later-init", "entity-1", "Init", conflictAt.AddMinutes(2), new JObject { ["Value"] = 300 }));
        if (reverse) db.History.Reverse();
        var retained = db.History.Where(entry => entry.Timestamp >= conflictAt).ToList();
        var retainedJson = JArray.FromObject(retained).ToString();

        var result = (await db.Rebase(surface: "client")).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("conflicting baseline timestamp").And.Contain(conflictAt.ToString("O"));
        result.Warning.Should().BeNull();
        result.ArchivedTrackingEntryCount.Should().Be(prefix ? 2 : 0);
        result.RebasedTo.Should().Be(prefix ? Harness.Start.AddMinutes(1) : null);
        JArray.FromObject(db.History.Where(entry => entry.Timestamp >= conflictAt)).ToString().Should().Be(retainedJson);
        db.Deleted.Should().NotIntersectWith(retained);
        if (prefix)
        {
            var baseline = db.History.Single(entry => entry.Timestamp < conflictAt);
            baseline.Data.Value<int>("Value").Should().Be(1);
            baseline.Data.DateTimeOffsetValueRequired("createdOn").Should().Be(Harness.Start);
        }
        else db.CheckpointCalls.Should().Be(0);
        var beforeRetry = JArray.FromObject(db.History).ToString();
        var retry = (await db.Rebase()).Single();
        retry.Success.Should().BeFalse();
        retry.ArchivedTrackingEntryCount.Should().Be(0);
        JArray.FromObject(db.History).ToString().Should().Be(beforeRetry);
    }

    [Fact]
    public async Task Earlier_patch_failure_takes_precedence_over_a_later_baseline_conflict()
    {
        var db = new Harness(true);
        db.History[1].Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");
        foreach (var value in new[] { 100, 200 })
            db.History.Add(db.Entry($"conflict-{value}", "entity-1", "Init", Harness.Start.AddMinutes(2), new JObject { ["Value"] = value }));
        var original = JArray.FromObject(db.History).ToString();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain(Harness.Start.AddMinutes(1).ToString("O"));
        result.ArchivedTrackingEntryCount.Should().Be(0);
        JArray.FromObject(db.History).ToString().Should().Be(original);
    }

    [Theory]
    [InlineData("later-history")]
    [InlineData("newer-stored-entity")]
    [InlineData("unknown-stored-time")]
    public async Task Historical_conflicts_preserve_history_when_stored_recovery_is_unsafe(string reason)
    {
        var stored = Stored();
        var cutoff = Harness.Start.AddMinutes(1);
        if (reason == "newer-stored-entity") stored["lastUpdated"] = cutoff.AddMinutes(1);
        if (reason == "unknown-stored-time") stored.Remove("lastUpdated");
        var db = Conflict(false, stored);
        if (reason == "later-history")
            db.History.Add(db.Entry("later-update", "entity-1", "Update", cutoff.AddMinutes(1), JObject.Parse("{ 'Status': ['tracked','later'] }")));
        var original = JArray.FromObject(db.History).ToString();

        var result = (await db.Rebase(cutoff)).Single();

        result.Success.Should().BeFalse();
        result.Warning.Should().BeNull();
        db.CheckpointCalls.Should().Be(0);
        db.MaterializeCalls.Should().Be(0);
        JArray.FromObject(db.History).ToString().Should().Be(original);
    }

    [Fact]
    public async Task A_stored_snapshot_confirmed_at_the_historical_cutoff_can_recover_conflicts()
    {
        var db = Conflict(false, Stored());

        var result = (await db.Rebase(Harness.Start.AddMinutes(1))).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().Contain("STORED_ENTITY_RECOVERY");
        db.History.Should().ContainSingle().Which.Data.Value<int>("Value").Should().Be(17);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_conflict_beyond_the_cutoff_does_not_affect_valid_historical_reconstruction(bool source)
    {
        var db = new Harness(source);
        foreach (var value in new[] { 100, 200 })
            db.History.Add(db.Entry($"conflict-{value}", "entity-1", "Init", Harness.Start.AddMinutes(2), new JObject { ["Value"] = value }));
        var retained = db.History.Where(entry => entry.Timestamp > Harness.Start.AddMinutes(1)).ToList();

        var result = (await db.Rebase(Harness.Start.AddMinutes(1))).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().BeNull();
        result.ArchivedTrackingEntryCount.Should().Be(2);
        db.History.Should().Contain(retained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unrelated_entities_continue_when_conflicting_history_cannot_be_recovered(bool source)
    {
        var db = Conflict(source, stored: null);
        var original = db.History.ToList();
        db.Seed("entity-2");

        var results = await db.Rebase();

        results.Single(result => result.EntityId == "entity-1").Success.Should().BeFalse();
        results.Single(result => result.EntityId == "entity-2").Success.Should().BeTrue();
        db.History.Where(entry => entry.EntityId == "entity-1").Should().Equal(original);
        db.Deleted.Should().OnlyContain(entry => entry.EntityId == "entity-2");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_unknown_recovery_never_removes_conflicts_or_reports_a_recovery_warning(bool unknown)
    {
        var db = Conflict(false, Stored());
        db.RejectCheckpoint = _ => new() { OutcomeUnknown = unknown, FailureReason = "unconfirmed" };
        var original = JArray.FromObject(db.History).ToString();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.Warning.Should().BeNull();
        result.ArchivedTrackingEntryCount.Should().Be(0);
        JArray.FromObject(db.History).ToString().Should().Be(original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Identical_baselines_with_later_updates_continue_using_normal_reconstruction(bool source)
    {
        var db = new Harness(source) { MaterializedEntities = [Stored()] };
        db.History.Add(db.Entry("identical-init", "entity-1", "Init", Harness.Start, (JObject)db.History[0].Data.DeepClone()));

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().BeNull();
        db.History.Should().ContainSingle().Which.Data.Value<int>("Value").Should().Be(1);
    }

    [Fact]
    public async Task Long_conflicting_history_recovers_in_atomic_chunks_without_replaying_either_baseline()
    {
        var db = Conflict(false, Stored());
        db.History.RemoveAll(entry => entry.EntryType == "Update");
        for (var index = 1; index <= 250; index++)
            db.History.Add(db.Entry($"update-{index}", "entity-1", "Update", Harness.Start.AddMinutes(index), new JObject { ["Value"] = new JArray(index - 1, index) }));
        db.RejectCheckpoint = checkpoint =>
        {
            if (db.CheckpointCalls > 1)
            {
                checkpoint.Baseline.Should().BeNull();
                ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(17);
                db.History.Count(entry => entry.EntryType == "Init").Should().Be(1);
            }
            return null;
        };

        var result = (await db.Rebase(includeArchivedTrackingEntries: false)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().Contain("STORED_ENTITY_RECOVERY");
        result.ArchivedTrackingEntryCount.Should().Be(252);
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.CheckpointCalls.Should().Be(3);
        db.History.Should().ContainSingle().Which.Data.Value<int>("Value").Should().Be(17);
        db.MaterializeCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_only_falls_back_to_tracking_creation_dates_that_agree(bool conflictingDates)
    {
        var stored = Stored();
        stored.Remove("createdOn");
        var db = Conflict(false, stored);
        var baselines = db.History.Where(entry => entry.EntryType == "Init").ToList();
        baselines[0].Data["createdOn"] = Harness.Start.AddDays(-1);
        baselines[1].Data["createdOn"] = Harness.Start.AddDays(conflictingDates ? -2 : -1);

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().Contain("STORED_ENTITY_RECOVERY");
        if (conflictingDates) db.History.Single().Data.ContainsKey("createdOn").Should().BeFalse();
        else db.History.Single().Data.DateTimeOffsetValueRequired("createdOn").Should().Be(Harness.Start.AddDays(-1));
    }

    private static JObject Stored() => new()
    {
        ["id"] = "entity-1", ["entityType"] = "Thing", ["Value"] = 17, ["Status"] = "stored",
        ["createdOn"] = Harness.Start.AddDays(-1), ["lastUpdated"] = Harness.Start.AddMinutes(1)
    };

    private static Harness Conflict(bool source, JObject? stored)
    {
        var db = new Harness(source) { MaterializedEntities = stored == null ? [] : [stored] };
        db.History[1].Data = JObject.Parse("{ 'Status': ['tracked'] }");
        db.History.Add(db.Entry("conflicting-init", "entity-1", ChangeTrackingEntryTypes.Init,
            Harness.Start, new JObject { ["Value"] = 200 }));
        return db;
    }
}
