using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Xunit;
using Harness = Reimaginate.DataHub.Test.Unit.Tests.RebaseTrackingPersistenceTests.Harness;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class StoredEntityRebaseRecoveryTests
{
    [Theory]
    [InlineData("internal", 1)]
    [InlineData("client", 121)]
    [InlineData("cli", 240)]
    public async Task Damaged_long_history_recovers_to_the_stored_snapshot_with_a_warning(string surface, int failedMinute)
    {
        var stored = Stored();
        var original = stored.ToString();
        var db = History(stored, failedMinute);
        var archived = db.History.ToList();
        var checkpoints = 0;
        db.RejectCheckpoint = checkpoint =>
        {
            if (++checkpoints > 1)
            {
                checkpoint.Baseline.Should().BeNull();
                // Every intermediate database state already represents the authoritative snapshot.
                ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(17);
                checkpoint.EntriesToDelete.Should().OnlyContain(entry => entry.Timestamp < Harness.Start.AddMinutes(250));
            }
            return null;
        };

        var result = (await db.Rebase(surface: surface)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.FailureReason.Should().BeNull();
        result.Warning.Should().Contain("STORED_ENTITY_RECOVERY").And.Contain("tracking may have been discarded");
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(250));
        result.ArchivedTrackingEntries.Should().BeEquivalentTo(archived);
        checkpoints.Should().Be(3);
        db.History.Should().ContainSingle();
        var expected = ChangeTrackingHelper.StripBaseProperties(stored);
        expected["createdOn"] = stored["createdOn"]!.DeepClone();
        JToken.DeepEquals(db.History[0].Data, expected).Should().BeTrue();
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(17);
        stored.ToString().Should().Be(original);
        db.MaterializedWrites.Should().BeEmpty();
        var serialized = JsonConvert.SerializeObject(result);
        JsonConvert.DeserializeObject<RebaseEntityTrackingResult>(serialized)!.Warning.Should().Be(result.Warning);
    }

    [Theory]
    [InlineData(-5, false)]
    [InlineData(251, false)]
    [InlineData(251, true)]
    [InlineData(null, false)]
    public async Task Recovery_does_not_equate_processing_time_with_event_time(int? storedMinute, bool explicitCutoff)
    {
        var stored = Stored();
        stored["lastUpdated"] = storedMinute.HasValue ? new JValue(Harness.Start.AddMinutes(storedMinute.Value)) : JValue.CreateNull();
        var db = History(stored);

        var result = (await db.Rebase(explicitCutoff ? Harness.Start.AddMinutes(300) : null)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().NotBeNullOrWhiteSpace();
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(250));
        db.History[0].Data.Value<int>("Value").Should().Be(17);
    }

    [Fact]
    public async Task Recovery_handles_missing_init_and_removes_every_same_timestamp_update_atomically()
    {
        var db = History(Stored());
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        var atCutoff = db.Entry("same-time-array-update", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start.AddMinutes(250), JObject.Parse("{ 'Items': { '_t': 'a', '1': ['B'] } }"));
        db.History.Add(atCutoff);
        db.RejectCheckpoint = checkpoint =>
        {
            if (checkpoint.Baseline != null)
                checkpoint.EntriesToDelete.Should().Contain(atCutoff);
            return null;
        };

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().NotBeNullOrWhiteSpace();
        ChangeTrackingHelper.ReassembleEntity(db.History)["Items"]!.Values<string>().Should().Equal("A", "B");
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("unknown")]
    [InlineData("too-large")]
    public async Task Unconfirmed_recovery_never_reports_success_or_a_recovery_warning(string outcome)
    {
        var db = History(Stored());
        var original = JArray.FromObject(db.History).ToString();
        db.RejectCheckpoint = _ => new()
        {
            FailureReason = "injected checkpoint rejection", OutcomeUnknown = outcome == "unknown", TooLarge = outcome == "too-large"
        };

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.Warning.Should().BeNull();
        result.RebasedTo.Should().BeNull();
        result.ArchivedTrackingEntries.Should().BeEmpty();
        JArray.FromObject(db.History).ToString().Should().Be(original);
        if (outcome != "too-large") db.CheckpointCalls.Should().Be(1);
    }

    [Fact]
    public async Task Failed_cleanup_leaves_a_valid_snapshot_and_retry_completes_without_replaying_damaged_entries()
    {
        var db = History(Stored());
        db.RejectCheckpoint = checkpoint => checkpoint.Baseline == null ? new() { FailureReason = "cleanup failed" } : null;

        var partial = (await db.Rebase()).Single();

        partial.Success.Should().BeFalse();
        partial.Warning.Should().NotBeNullOrWhiteSpace();
        partial.RebasedTo.Should().Be(Harness.Start.AddMinutes(250));
        partial.ArchivedTrackingEntries.Should().HaveCount(99);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(17);
        db.RejectCheckpoint = null;

        var retry = (await db.Rebase()).Single();

        retry.Success.Should().BeTrue(retry.FailureReason);
        db.History.Should().ContainSingle();
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(17);
    }

    [Fact]
    public async Task Recovery_reduces_payloads_only_after_definite_size_rejections()
    {
        var db = History(Stored());
        db.RejectCheckpoint = checkpoint => checkpoint.EntriesToDelete.Count > 10 ? new() { TooLarge = true } : null;

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().NotBeNullOrWhiteSpace();
        result.ArchivedTrackingEntries.Should().HaveCount(251);
        db.History.Should().ContainSingle();
    }

    [Theory]
    [InlineData("later-history")]
    [InlineData("newer-stored-entity")]
    [InlineData("unknown-stored-time")]
    public async Task A_current_snapshot_cannot_be_substituted_for_an_unavailable_historical_state(string reason)
    {
        var stored = Stored();
        if (reason == "unknown-stored-time") stored.Remove("lastUpdated");
        var db = History(stored);
        var original = JArray.FromObject(db.History).ToString();
        var cutoff = reason == "later-history" ? Harness.Start.AddMinutes(200) : Harness.Start.AddMinutes(250);

        var result = (await db.Rebase(cutoff)).Single();

        result.Success.Should().BeFalse();
        result.Warning.Should().BeNull();
        db.CheckpointCalls.Should().Be(0);
        JArray.FromObject(db.History).ToString().Should().Be(original);
    }

    [Fact]
    public async Task An_oversized_required_transaction_preserves_history_and_unrelated_entities_continue()
    {
        var db = History(Stored());
        for (var index = 0; index < 100; index++)
            db.History.Add(db.Entry($"same-time-{index}", "entity-1", ChangeTrackingEntryTypes.Update,
                Harness.Start.AddMinutes(250), new JObject()));
        var original = db.History.ToList();
        db.Seed("entity-2");

        var results = await db.Rebase();

        results.Single(result => result.EntityId == "entity-1").Success.Should().BeFalse();
        results.Single(result => result.EntityId == "entity-1").Warning.Should().BeNull();
        results.Single(result => result.EntityId == "entity-2").Success.Should().BeTrue();
        db.History.Where(entry => entry.EntityId == "entity-1").Should().Equal(original);
    }

    [Fact]
    public async Task Healthy_tracking_uses_reconstructed_data_without_a_recovery_warning()
    {
        var db = new Harness(false) { MaterializedEntities = [Stored()] };

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue();
        result.Warning.Should().BeNull();
        db.History[0].Data.Value<int>("Value").Should().Be(1);
    }

    private static JObject Stored() => new()
    {
        ["id"] = "entity-1", ["entityType"] = "Thing", ["Value"] = 17,
        ["Items"] = new JArray("A", "B"), ["Optional"] = JValue.CreateNull(),
        ["createdOn"] = Harness.Start.AddDays(-1), ["lastUpdated"] = Harness.Start.AddMinutes(251)
    };

    private static Harness History(JObject stored, int failedMinute = 121)
    {
        var db = new Harness(false) { MaterializedEntities = [stored] };
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Update);
        for (var index = 1; index <= 250; index++)
            db.History.Add(db.Entry($"update-{index}", "entity-1", ChangeTrackingEntryTypes.Update,
                Harness.Start.AddMinutes(index), new JObject { ["Value"] = new JArray(index - 1, index) }));
        db.History.Single(entry => entry.id == $"update-{failedMinute}").Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");
        return db;
    }
}
