using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Xunit;
using Harness = Reimaginate.DataHub.Test.Unit.Tests.RebaseTrackingPersistenceTests.Harness;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class AtomicRebaseCheckpointTests
{
    [Theory]
    [InlineData("client")]
    [InlineData("cli")]
    public async Task Public_data_hub_results_report_the_confirmed_partial_cutoff(string surface)
    {
        var db = LongHistory(false);
        db.RejectCheckpoint = checkpoint => checkpoint.Baseline.Timestamp > Harness.Start.AddMinutes(98)
            ? new() { FailureReason = "injected checkpoint failure" } : null;

        var result = (await db.Rebase(Harness.Start.AddMinutes(250), surface)).Single();

        result.Success.Should().BeFalse();
        result.DataSource.Should().Be(DataSources.DataHub);
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(98));
        result.ArchivedTrackingEntries.Should().HaveCount(99);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Long_history_advances_through_atomic_checkpoints_and_reports_only_original_entries(bool source)
    {
        var db = LongHistory(source);
        var original = db.History.ToList();
        var checkpoints = new List<CommitRebaseCheckpointCommand>();
        db.RejectCheckpoint = checkpoint => { checkpoints.Add(checkpoint); return null; };

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(250));
        result.ArchivedTrackingEntries.Should().BeEquivalentTo(original);
        checkpoints.Should().HaveCount(3);
        checkpoints.Should().OnlyContain(checkpoint => checkpoint.EntriesToDelete.Count + 1 <= 100);
        db.History.Should().ContainSingle();
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(250);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_later_checkpoint_leaves_a_valid_prefix_and_retry_finishes_without_duplicate_array_changes(bool source)
    {
        var db = LongHistory(source, arrays: true);
        db.RejectCheckpoint = checkpoint => checkpoint.Baseline.Timestamp > Harness.Start.AddMinutes(98)
            ? new() { FailureReason = "injected later checkpoint failure" } : null;

        var partial = (await db.Rebase()).Single();

        partial.Success.Should().BeFalse();
        partial.RebasedTo.Should().Be(Harness.Start.AddMinutes(98));
        partial.ArchivedTrackingEntries.Should().HaveCount(99);
        ChangeTrackingHelper.ReassembleEntity(db.History)["Items"]!.Values<int>().Should().Equal(Enumerable.Range(1, 250));
        db.RejectCheckpoint = null;

        var retry = (await db.Rebase()).Single();

        retry.Success.Should().BeTrue(retry.FailureReason);
        ChangeTrackingHelper.ReassembleEntity(db.History)["Items"]!.Values<int>().Should().Equal(Enumerable.Range(1, 250));
        db.History.Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_atomic_deletion_keeps_exactly_the_original_history_and_replay(bool source)
    {
        var db = new Harness(source) { FailDeleteId = "entity-1-update" };
        db.History[0].Data = JObject.Parse("{ 'Items': ['A'] }");
        db.History[1].Data = JObject.Parse("{ 'Items': { '_t': 'a', '1': ['B'] } }");
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.RebasedTo.Should().BeNull();
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Equal(original);
        ChangeTrackingHelper.ReassembleEntity(db.History)["Items"]!.Values<string>().Should().Equal("A", "B");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_timestamp_group_that_cannot_fit_is_preserved_and_other_entities_continue(bool source)
    {
        var db = new Harness(source);
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Update);
        for (var index = 1; index <= 100; index++)
            db.History.Add(db.Entry($"same-time-{index}", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(1),
                new JObject { ["Value"] = new JArray(index - 1, index) }));
        var original = db.History.ToList();
        db.Seed("entity-2");

        var results = await db.Rebase();

        results.Single(result => result.EntityId == "entity-1").Success.Should().BeFalse();
        results.Single(result => result.EntityId == "entity-2").Success.Should().BeTrue();
        db.History.Where(entry => entry.EntityId == "entity-1").Should().Equal(original);
        ChangeTrackingHelper.ReassembleEntity(original).Value<int>("Value").Should().Be(100);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Payload_limit_reduces_the_checkpoint_at_complete_timestamp_boundaries(bool source)
    {
        var db = LongHistory(source);
        var committedCutoffs = new List<DateTimeOffset>();
        db.RejectCheckpoint = checkpoint =>
        {
            if (checkpoint.EntriesToDelete.Count > 25) return new() { TooLarge = true };
            committedCutoffs.Add(checkpoint.Baseline.Timestamp);
            return null;
        };

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        committedCutoffs.Should().HaveCountGreaterThan(3).And.BeInAscendingOrder();
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(250);
    }

    [Fact]
    public async Task Failed_source_timestamp_group_and_its_tail_survive_multiple_checkpoints()
    {
        var db = LongHistory(true);
        db.History.Single(entry => entry.id == "update-121").Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");
        var tail = db.History.Where(entry => entry.Timestamp >= Harness.Start.AddMinutes(121)).ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("Partial rebase");
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(120));
        db.History.Where(entry => entry.EntryType == ChangeTrackingEntryTypes.Update).Should().Equal(tail);
        db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init).Data.Value<int>("Value").Should().Be(120);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unconfirmed_checkpoint_stops_that_entity_and_does_not_retry_automatically(bool source)
    {
        var db = LongHistory(source);
        var ids = new List<string>();
        db.RejectCheckpoint = checkpoint =>
        {
            ids.Add(checkpoint.Baseline.id);
            return new() { OutcomeUnknown = true, FailureReason = "outcome unknown" };
        };
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        ids.Should().ContainSingle();
        db.History.Should().Equal(original);
        await db.Rebase();
        ids.Should().HaveCount(2);
        ids[0].Should().Be(ids[1], "the same checkpoint must have the same identity on an explicit retry");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Oversized_baseline_never_starts_a_partial_rebase(bool source)
    {
        var db = LongHistory(source);
        db.RejectCheckpoint = _ => new() { TooLarge = true };
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.RebasedTo.Should().BeNull();
        db.History.Should().Equal(original);
    }

    private static Harness LongHistory(bool source, bool arrays = false)
    {
        var db = new Harness(source);
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Update);
        if (arrays) db.History[0].Data = new JObject { ["Items"] = new JArray() };
        for (var index = 1; index <= 250; index++)
        {
            var patch = arrays
                ? new JObject { ["Items"] = new JObject { ["_t"] = "a", [(index - 1).ToString()] = new JArray(index) } }
                : new JObject { ["Value"] = new JArray(index - 1, index) };
            db.History.Add(db.Entry($"update-{index}", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(index), patch));
        }
        return db;
    }
}
