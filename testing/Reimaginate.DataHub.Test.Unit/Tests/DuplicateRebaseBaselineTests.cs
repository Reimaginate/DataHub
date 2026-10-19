using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Xunit;
using Harness = Reimaginate.DataHub.Test.Unit.Tests.RebaseTrackingPersistenceTests.Harness;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DuplicateRebaseBaselineTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task Identical_baselines_are_deduplicated_without_rewriting_the_retained_snapshot(bool source, bool include, bool reverse)
    {
        var db = Duplicates(source);
        if (reverse) db.History.Reverse();
        var before = ChangeTrackingHelper.ReassembleEntity(db.History);
        var retained = db.History.Last();
        var removed = db.History.First();
        var originalEntries = db.History.ToList();
        var original = JArray.FromObject(db.History).ToString();

        var result = (await db.Rebase(surface: source ? "client" : "cli", includeArchivedTrackingEntries: include)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().BeNull();
        result.RebasedTo.Should().Be(Harness.Start);
        result.ArchivedTrackingEntryCount.Should().Be(1);
        if (include) result.ArchivedTrackingEntries.Should().Equal(removed);
        else result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().ContainSingle().Which.Should().BeSameAs(retained);
        JToken.DeepEquals(ChangeTrackingHelper.ReassembleEntity(db.History), before).Should().BeTrue();
        // Neither snapshot's data is mutated, including nulls, arrays and creation metadata.
        JArray.FromObject(originalEntries).ToString().Should().Be(original);
        db.CreateCalls.Should().Be(0);
        db.History.Add(db.Entry("later-update", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start.AddMinutes(1), JObject.Parse("{ 'Value': [0,1] }")));
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Large_duplicate_groups_can_be_cleaned_up_in_chunks_while_older_inits_are_removed_together(bool source)
    {
        var db = Duplicates(source, copies: 251);
        var retained = db.History.Last();
        var olderInits = Enumerable.Range(1, 3).Select(i => db.Entry($"older-init-{i}", "entity-1", ChangeTrackingEntryTypes.Init,
            Harness.Start.AddMinutes(-i), new JObject { ["Value"] = -i })).ToList();
        db.History.AddRange(olderInits);
        for (var i = 1; i <= 150; i++) db.History.Add(db.Entry($"older-update-{i}", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start.AddSeconds(-i), new JObject()));
        var expectedCount = db.History.Count - 1;
        db.RejectCheckpoint = checkpoint =>
        {
            checkpoint.Baseline.Should().BeNull();
            checkpoint.EntriesToDelete.Should().HaveCountLessThanOrEqualTo(100).And.NotContain(retained);
            if (db.CheckpointCalls == 1)
                checkpoint.EntriesToDelete.Should().Contain(olderInits);
            else
            {
                db.History.Should().NotContain(entry => entry.EntryType == ChangeTrackingEntryTypes.Init && entry.Timestamp < Harness.Start);
                ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(0);
            }
            return null;
        };

        var result = (await db.Rebase(includeArchivedTrackingEntries: false)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.ArchivedTrackingEntryCount.Should().Be(expectedCount);
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.CheckpointCalls.Should().Be(5);
        db.History.Should().ContainSingle().Which.Should().BeSameAs(retained);
        db.CreateCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false, "value")]
    [InlineData(true, "value")]
    [InlineData(false, "array-order")]
    [InlineData(true, "array-order")]
    [InlineData(false, "missing-null")]
    [InlineData(true, "missing-null")]
    [InlineData(false, "createdOn")]
    [InlineData(true, "createdOn")]
    public async Task Conflicting_snapshot_data_without_an_authoritative_stored_entity_is_preserved(bool source, string difference)
    {
        var db = Duplicates(source);
        var data = db.History.Last().Data;
        switch (difference)
        {
            case "value": data["Value"] = 2; break;
            case "array-order": data["Items"] = new JArray("B", JValue.CreateNull(), "A"); break;
            case "missing-null": data.Remove("Optional"); break;
            case "createdOn": data["createdOn"] = Harness.Start.AddDays(-2); break;
        }
        var original = JArray.FromObject(db.History).ToString();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain(source ? "separate recovery" : "NOT_FOUND");
        result.Warning.Should().BeNull();
        result.ArchivedTrackingEntryCount.Should().Be(0);
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.CheckpointCalls.Should().Be(0);
        JArray.FromObject(db.History).ToString().Should().Be(original);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task Unconfirmed_cleanup_preserves_the_retained_snapshot_and_can_be_retried(bool source, int failingCheckpoint)
    {
        var db = Duplicates(source, copies: 251);
        var retained = db.History.Last();
        db.RejectCheckpoint = _ => db.CheckpointCalls == failingCheckpoint ? new() { FailureReason = "injected cleanup failure" } : null;

        var partial = (await db.Rebase()).Single();

        partial.Success.Should().BeFalse();
        partial.ArchivedTrackingEntryCount.Should().Be(failingCheckpoint == 1 ? 0 : 100);
        db.History.Should().Contain(retained);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(0);
        db.RejectCheckpoint = null;

        var retry = (await db.Rebase()).Single();

        retry.Success.Should().BeTrue(retry.FailureReason);
        retry.ArchivedTrackingEntryCount.Should().Be(failingCheckpoint == 1 ? 250 : 150);
        db.History.Should().ContainSingle().Which.Should().BeSameAs(retained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Updates_sharing_the_baseline_timestamp_still_require_reconstruction(bool source)
    {
        var db = Duplicates(source);
        db.History.Add(db.Entry("same-time-update", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start, JObject.Parse("{ 'Value': [0,1] }")));

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.ArchivedTrackingEntryCount.Should().Be(3);
        db.History.Should().ContainSingle();
        db.CreateCalls.Should().Be(1);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(1);
    }

    [Fact]
    public async Task Conflicting_baselines_use_stored_entity_recovery_without_replaying_a_damaged_update()
    {
        var stored = new JObject
        {
            ["id"] = "entity-1", ["entityType"] = "Thing", ["Value"] = 17,
            ["createdOn"] = Harness.Start.AddDays(-1), ["lastUpdated"] = Harness.Start.AddMinutes(1)
        };
        var db = new Harness(false) { MaterializedEntities = [stored] };
        db.History[1].Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");
        db.History.Add(db.Entry("conflicting-init", "entity-1", ChangeTrackingEntryTypes.Init,
            Harness.Start, new JObject { ["Value"] = 2 }));

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().Contain("STORED_ENTITY_RECOVERY");
        db.MaterializeCalls.Should().Be(0);
        result.ArchivedTrackingEntryCount.Should().Be(3);
        db.History.Should().ContainSingle();
        db.History[0].Data.Value<int>("Value").Should().Be(17);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_deduplication_preserves_later_updates(bool source)
    {
        var db = Duplicates(source);
        var later = db.Entry("later-update", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start.AddMinutes(1), JObject.Parse("{ 'Value': [0,1] }"));
        db.History.Add(later);

        var result = (await db.Rebase(Harness.Start)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.ArchivedTrackingEntryCount.Should().Be(1);
        db.History.Should().Contain(later).And.ContainSingle(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(1);
    }

    [Fact]
    public async Task Source_partial_rebase_can_deduplicate_the_valid_prefix_without_removing_the_failure()
    {
        var db = Duplicates(true);
        var failed = db.Entry("bad-update", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start.AddMinutes(1), JObject.Parse("{ 'Value': [0,1,2,3,4] }"));
        db.History.Add(failed);

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("Partial rebase");
        result.ArchivedTrackingEntryCount.Should().Be(1);
        result.RebasedTo.Should().Be(Harness.Start);
        db.History.Should().Contain(failed).And.ContainSingle(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
    }

    private static Harness Duplicates(bool source, int copies = 2)
    {
        var db = new Harness(source) { MaterializedEntities = [] };
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Update);
        var data = JObject.Parse("{ 'Value': 0, 'Items': ['A',null,'B'], 'Optional': null, 'Nested': { 'Name': 'keep' } }");
        data["createdOn"] = Harness.Start.AddDays(-1);
        db.History[0].Data = data;
        for (var i = 1; i < copies; i++) db.History.Add(db.Entry($"duplicate-{i}", "entity-1", ChangeTrackingEntryTypes.Init,
            Harness.Start, new JObject(data.Properties().Reverse().Select(property => new JProperty(property.Name, property.Value.DeepClone())))));
        return db;
    }
}
