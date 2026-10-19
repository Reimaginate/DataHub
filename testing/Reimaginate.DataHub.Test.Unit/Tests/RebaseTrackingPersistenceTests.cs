using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OneOf;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.DataAccess.Queries.GetSourceRebaseIdentities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;
using Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;
using Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;
using Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class RebaseTrackingPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Successful_rebase_saves_a_baseline_before_deleting_original_history(bool source)
    {
        var db = new Harness(source);
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue();
        result.ArchivedTrackingEntries.Should().BeEquivalentTo(original);
        db.History.Should().ContainSingle(e => e.EntryType == ChangeTrackingEntryTypes.Init);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_baseline_save_preserves_history_and_reports_the_error(bool source)
    {
        var db = new Harness(source) { FailCreateFor = "entity-1" };
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("injected baseline failure");
        result.ArchivedTrackingEntries.Should().BeEmpty();
        result.RebasedTo.Should().BeNull();
        db.History.Should().Equal(original);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mixed_save_results_only_delete_history_for_confirmed_baselines(bool source)
    {
        var db = new Harness(source) { FailCreateFor = "entity-1" };
        db.Seed("entity-2");
        var failedHistory = db.History.Where(e => e.EntityId == "entity-1").ToList();
        var successfulHistory = db.History.Where(e => e.EntityId == "entity-2").ToList();

        var results = await db.Rebase();

        results.Single(r => r.EntityId == "entity-1").Success.Should().BeFalse();
        var success = results.Single(r => r.EntityId == "entity-2");
        success.Success.Should().BeTrue();
        success.ArchivedTrackingEntries.Should().BeEquivalentTo(successfulHistory);
        db.History.Where(e => e.EntityId == "entity-1").Should().Equal(failedHistory);
        db.History.Where(e => e.EntityId == "entity-2").Should().ContainSingle(e => e.EntryType == ChangeTrackingEntryTypes.Init);
        db.Deleted.Should().OnlyContain(e => e.EntityId == "entity-2");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unconfirmed_baseline_save_preserves_original_history(bool source)
    {
        var db = new Harness(source) { OmitCreateConfirmation = true };
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("not confirmed");
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Equal(original);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_checkpoint_rolls_back_baseline_and_all_deletions_for_that_entity(bool source)
    {
        var db = new Harness(source) { FailDeleteId = "entity-1-update" };
        db.Seed("entity-2");
        var originalInit = db.History.Single(e => e.id == "entity-1-init");

        var results = await db.Rebase();

        var failure = results.Single(r => r.EntityId == "entity-1");
        failure.Success.Should().BeFalse();
        failure.FailureReason.Should().Contain("injected delete failure");
        failure.ArchivedTrackingEntries.Should().BeEmpty();
        results.Single(r => r.EntityId == "entity-2").Success.Should().BeTrue();
        db.History.Should().Contain(e => e.id == db.FailDeleteId);
        db.History.Should().Contain(originalInit);
        db.History.Should().NotContain(e => e.EntityId == "entity-1" && e.id.StartsWith("replacement-"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unconfirmed_deletion_is_reported_as_a_failure(bool source)
    {
        var db = new Harness(source) { OmitDeleteConfirmation = true };
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("not confirmed");
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Contain(original);
        db.History.Should().NotContain(e => e.id.StartsWith("replacement-"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_baseline_can_be_retained_without_a_replacement_write(bool source)
    {
        var db = new Harness(source);
        var retainedInit = db.Entry("retained-init", "entity-1", ChangeTrackingEntryTypes.Init, Harness.Start.AddMinutes(2), new JObject { ["Value"] = 1 });
        db.History.Add(retainedInit);
        var expectedDeleted = db.History.Where(e => e != retainedInit).ToList();

        var result = (await db.Rebase(retainedInit.Timestamp)).Single();

        result.Success.Should().BeTrue();
        result.ArchivedTrackingEntries.Should().BeEquivalentTo(expectedDeleted);
        db.History.Should().Equal(retainedInit);
        db.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task Source_history_without_a_baseline_is_preserved_and_reported_as_a_failure()
    {
        var db = new Harness(true);
        db.History.RemoveAll(e => e.EntryType == ChangeTrackingEntryTypes.Init);
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("initial");
        db.History.Should().Equal(original);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Thrown_write_error_preserves_history_and_returns_a_failure(bool source)
    {
        var db = new Harness(source) { ThrowOnCreate = true };
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("injected write exception");
        db.History.Should().Equal(original);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Thrown_transaction_delete_error_preserves_the_original_history(bool source)
    {
        var db = new Harness(source) { ThrowOnDelete = true };
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("injected delete exception");
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Contain(original);
        db.History.Should().NotContain(e => e.id.StartsWith("replacement-"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_before_atomic_commit_preserves_original_history(bool source)
    {
        var db = new Harness(source) { CancelAfterCreate = true };
        var original = db.History.ToList();

        var action = async () => await db.Rebase();

        await action.Should().ThrowAsync<OperationCanceledException>();
        db.History.Should().Contain(original);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmation_for_another_data_source_cannot_authorize_history_deletion(bool source)
    {
        var db = new Harness(source) { WrongCreateConfirmation = true };
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("not confirmed");
        db.History.Should().Contain(original);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retained_baseline_can_complete_when_another_entity_replacement_fails(bool source)
    {
        var db = new Harness(source) { FailCreateFor = "entity-1" };
        db.Seed("entity-2");
        var retainedInit = db.Entry("retained-init", "entity-2", ChangeTrackingEntryTypes.Init, Harness.Start.AddMinutes(2), new JObject { ["Value"] = 1 });
        db.History.Add(retainedInit);
        var failedHistory = db.History.Where(e => e.EntityId == "entity-1").ToList();

        var results = await db.Rebase(retainedInit.Timestamp);

        results.Single(r => r.EntityId == "entity-1").Success.Should().BeFalse();
        results.Single(r => r.EntityId == "entity-2").Success.Should().BeTrue();
        db.History.Where(e => e.EntityId == "entity-1").Should().Equal(failedHistory);
        db.History.Where(e => e.EntityId == "entity-2").Should().Equal(retainedInit);
    }

    [Theory]
    [InlineData(false, 60)]
    [InlineData(false, 90)]
    [InlineData(true, 60)]
    [InlineData(true, 90)]
    public async Task Historical_rebase_uses_the_state_at_the_effective_cutoff_and_preserves_later_updates(bool source, int cutoffSeconds)
    {
        var db = new Harness(source);
        var laterUpdate = db.Entry("later-update", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(2), JObject.Parse("{ 'Value': [1,2] }"));
        db.History.Add(laterUpdate);
        var laterUpdateSnapshot = laterUpdate.Data.DeepClone();

        var result = (await db.Rebase(Harness.Start.AddSeconds(cutoffSeconds))).Single();

        result.Success.Should().BeTrue();
        var baseline = db.History.Single(e => e.EntryType == ChangeTrackingEntryTypes.Init);
        baseline.Timestamp.Should().Be(Harness.Start.AddMinutes(1));
        baseline.Data.Value<int>("Value").Should().Be(1);
        result.ArchivedTrackingEntries.Should().OnlyContain(e => e.Timestamp <= baseline.Timestamp);
        db.History.Should().Contain(laterUpdate);
        JToken.DeepEquals(laterUpdate.Data, laterUpdateSnapshot).Should().BeTrue();
        ChangeTrackingHelper.ReassembleEntity(db.History.Where(e => e.Timestamp <= baseline.Timestamp).ToList()).Value<int>("Value").Should().Be(1);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_rebase_preserves_nested_and_array_state_before_and_after_later_patches(bool source)
    {
        var db = new Harness(source);
        var initial = JObject.Parse("{ 'Details': { 'Name': 'initial' }, 'Items': ['a'], 'RemovedLater': 'keep' }");
        var atCutoff = JObject.Parse("{ 'Details': { 'Name': 'cutoff' }, 'Items': ['a','b'], 'RemovedLater': 'keep' }");
        var latest = JObject.Parse("{ 'Details': { 'Name': 'latest' }, 'Items': ['b','c'], 'AddedLater': true }");
        db.History.Clear();
        db.History.Add(db.Entry("initial", "entity-1", ChangeTrackingEntryTypes.Init, Harness.Start, initial));
        db.History.Add(db.Entry("at-cutoff", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(1), (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(initial, atCutoff)));
        var laterUpdate = db.Entry("after-cutoff", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(2), (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(atCutoff, latest));
        db.History.Add(laterUpdate);

        var result = (await db.Rebase(Harness.Start.AddMinutes(1))).Single();

        result.Success.Should().BeTrue();
        var baseline = db.History.Single(e => e.EntryType == ChangeTrackingEntryTypes.Init);
        JToken.DeepEquals(baseline.Data, new JObject(atCutoff) { ["createdOn"] = Harness.Start }).Should().BeTrue();
        JToken.DeepEquals(ChangeTrackingHelper.StripBaseProperties(ChangeTrackingHelper.ReassembleEntity(db.History)), latest).Should().BeTrue();
        db.History.Should().Contain(laterUpdate);
        result.ArchivedTrackingEntries.Should().NotContain(laterUpdate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_baseline_includes_all_updates_at_the_cutoff_timestamp(bool source)
    {
        var db = new Harness(source);
        db.History.Single(e => e.EntryType == ChangeTrackingEntryTypes.Update)._ts = 1;
        var sameTimeUpdate = db.Entry("same-time-update", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(1), JObject.Parse("{ 'Value': [1,2] }"));
        sameTimeUpdate._ts = 2;
        db.History.Add(sameTimeUpdate);
        db.History.Add(db.Entry("later-update", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(2), JObject.Parse("{ 'Value': [2,3] }")));

        var result = (await db.Rebase(Harness.Start.AddMinutes(1))).Single();

        result.Success.Should().BeTrue();
        db.History.Single(e => e.EntryType == ChangeTrackingEntryTypes.Init).Data.Value<int>("Value").Should().Be(2);
        db.History.Should().HaveCount(2);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Init_at_the_cutoff_is_rebuilt_when_updates_share_its_timestamp(bool source)
    {
        var db = new Harness(source);
        db.History.Single(e => e.EntryType == ChangeTrackingEntryTypes.Update).Timestamp = Harness.Start;
        db.History.Add(db.Entry("later-update", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(1), JObject.Parse("{ 'Value': [1,2] }")));

        var result = (await db.Rebase(Harness.Start)).Single();

        result.Success.Should().BeTrue();
        db.History.Single(e => e.EntryType == ChangeTrackingEntryTypes.Init).Data.Value<int>("Value").Should().Be(1);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_reconstruction_uses_the_latest_init_at_or_before_the_cutoff(bool source)
    {
        var db = new Harness(source);
        db.History.Add(db.Entry("newer-init", "entity-1", ChangeTrackingEntryTypes.Init, Harness.Start.AddMinutes(2), new JObject { ["Value"] = 5 }));
        db.History.Add(db.Entry("later-update", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(3), JObject.Parse("{ 'Value': [5,6] }")));

        var result = (await db.Rebase(Harness.Start.AddMinutes(2).AddSeconds(30))).Single();

        result.Success.Should().BeTrue();
        var baseline = db.History.Single(e => e.EntryType == ChangeTrackingEntryTypes.Init);
        baseline.Timestamp.Should().Be(Harness.Start.AddMinutes(2));
        baseline.Data.Value<int>("Value").Should().Be(5);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_historical_reconstruction_cannot_fall_back_to_the_current_stored_entity(bool hasLaterTracking)
    {
        var db = new Harness(false)
        {
            MaterializedEntities = [new JObject { ["id"] = "entity-1", ["entityType"] = "Thing", ["Value"] = 2, ["lastUpdated"] = Harness.Start.AddMinutes(2) }]
        };
        db.History.Single(e => e.EntryType == ChangeTrackingEntryTypes.Update).Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");
        if (hasLaterTracking)
            db.History.Add(db.Entry("later-update", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(2), JObject.Parse("{ 'Value': [1,2] }")));
        var original = db.History.ToList();

        var result = (await db.Rebase(Harness.Start.AddMinutes(1))).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("materialize");
        db.History.Should().Equal(original);
        db.CreateCalls.Should().Be(0);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_future_init_cannot_be_used_to_reconstruct_an_earlier_cutoff(bool source)
    {
        var db = new Harness(source);
        db.History.RemoveAll(e => e.EntryType == ChangeTrackingEntryTypes.Init);
        db.History.Add(db.Entry("future-init", "entity-1", ChangeTrackingEntryTypes.Init, Harness.Start.AddMinutes(2), new JObject { ["Value"] = 2 }));
        var original = db.History.ToList();

        var result = (await db.Rebase(Harness.Start.AddMinutes(1))).Single();

        result.Success.Should().BeFalse();
        db.History.Should().Equal(original);
        db.CreateCalls.Should().Be(0);
        db.DeleteCalls.Should().Be(0);
    }

    [Fact]
    public async Task Rebase_without_a_historical_cutoff_can_still_recover_from_the_stored_entity()
    {
        var db = new Harness(false)
        {
            MaterializedEntities = [new JObject { ["id"] = "entity-1", ["entityType"] = "Thing", ["Value"] = 2, ["lastUpdated"] = Harness.Start.AddMinutes(2) }]
        };
        db.History.Single(e => e.EntryType == ChangeTrackingEntryTypes.Update).Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue();
        db.History.Should().ContainSingle(e => e.EntryType == ChangeTrackingEntryTypes.Init);
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cutoff_before_the_first_entry_returns_a_successful_no_op(bool source)
    {
        var db = new Harness(source);
        var original = db.History.ToList();
        var originalJson = JArray.FromObject(db.History).ToString();

        var result = (await db.Rebase(Harness.Start.AddTicks(-1))).Single();

        result.Success.Should().BeTrue();
        result.FailureReason.Should().BeNull();
        result.ArchivedTrackingEntries.Should().BeEmpty();
        result.DataSource.Should().Be(original[0].DataSource);
        db.History.Should().Equal(original);
        JArray.FromObject(db.History).ToString().Should().Be(originalJson);
        db.MaterializeCalls.Should().Be(0);
        db.CreateCalls.Should().Be(0);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cutoff_before_an_existing_rebased_baseline_leaves_it_untouched(bool source)
    {
        var db = new Harness(source);
        (await db.Rebase()).Single().Success.Should().BeTrue();
        var baseline = db.History.Single();
        var createCalls = db.CreateCalls;
        var deleteCalls = db.DeleteCalls;
        var materializeCalls = db.MaterializeCalls;

        var result = (await db.Rebase(Harness.Start.AddSeconds(30))).Single();

        result.Success.Should().BeTrue();
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Equal(baseline);
        db.MaterializeCalls.Should().Be(materializeCalls);
        db.CreateCalls.Should().Be(createCalls);
        db.DeleteCalls.Should().Be(deleteCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_batch_can_rebase_eligible_history_and_skip_history_starting_after_the_cutoff(bool source)
    {
        var db = new Harness(source);
        db.Seed("entity-2");
        var skippedHistory = db.History.Where(entry => entry.EntityId == "entity-2").ToList();
        foreach (var entry in skippedHistory) entry.Timestamp = entry.Timestamp.AddMinutes(5);

        var results = await db.Rebase(Harness.Start.AddMinutes(1));

        var skipped = results.Single(result => result.EntityId == "entity-2");
        skipped.Success.Should().BeTrue();
        skipped.ArchivedTrackingEntries.Should().BeEmpty();
        var rebased = results.Single(result => result.EntityId == "entity-1");
        rebased.Success.Should().BeTrue();
        rebased.ArchivedTrackingEntries.Should().HaveCount(2);
        db.History.Where(entry => entry.EntityId == "entity-2").Should().Equal(skippedHistory);
        var rebasedHistory = db.History.Where(entry => entry.EntityId == "entity-1").ToList();
        rebasedHistory.Should().ContainSingle(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        ChangeTrackingHelper.ReassembleEntity(rebasedHistory).Value<int>("Value").Should().Be(1);
        db.Deleted.Should().OnlyContain(entry => entry.EntityId == "entity-1");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task History_after_the_cutoff_does_not_need_to_be_reconstructable_for_a_no_op(bool source)
    {
        var db = new Harness(source) { MaterializedEntities = [] };
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        var original = db.History.ToList();

        var result = (await db.Rebase(Harness.Start)).Single();

        result.Success.Should().BeTrue();
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Equal(original);
        db.MaterializeCalls.Should().Be(0);
        db.CreateCalls.Should().Be(0);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false, "flat")]
    [InlineData(true, "flat")]
    [InlineData(false, "nested")]
    [InlineData(true, "nested")]
    [InlineData(false, "all-null")]
    [InlineData(true, "all-null")]
    public async Task Rebase_preserves_null_properties_and_array_elements_in_the_replacement_snapshot(bool source, string shape)
    {
        var db = new Harness(source);
        db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init).Data = NullSnapshot(shape);
        var expected = ChangeTrackingHelper.StripBaseProperties(ChangeTrackingHelper.ReassembleEntity(db.History));
        var original = db.History.ToList();
        var originalJson = JArray.FromObject(original).ToString();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue();
        var baseline = db.History.Single();
        JToken.DeepEquals(baseline.Data, new JObject(expected) { ["createdOn"] = Harness.Start }).Should().BeTrue();
        JToken.DeepEquals(ChangeTrackingHelper.StripBaseProperties(ChangeTrackingHelper.ReassembleEntity(db.History)), expected).Should().BeTrue();
        JArray.FromObject(original).ToString().Should().Be(originalJson);
        db.MaterializedWrites.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task Retained_array_patches_replay_against_the_original_indexes_after_rebase(bool source, bool nested, bool deleteNull)
    {
        var db = new Harness(source);
        var data = NullSnapshot("flat");
        if (nested)
        {
            data = new JObject { ["Value"] = 0, ["Details"] = data };
        }
        db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init).Data = data;
        var atCutoff = ChangeTrackingHelper.StripBaseProperties(ChangeTrackingHelper.ReassembleEntity(db.History));
        var expected = (JObject)atCutoff.DeepClone();
        var expectedItems = (JArray)(nested ? expected["Details"]!["Items"]! : expected["Items"]!);
        if (deleteNull) expectedItems.RemoveAt(1);
        else expectedItems[2] = "updated";

        var arrayPatch = JObject.Parse(deleteNull
            ? "{ '_t': 'a', '_1': [null, 0, 0] }"
            : "{ '_t': 'a', '2': ['b', 'updated'] }");
        var patch = new JObject { ["Items"] = arrayPatch };
        if (nested) patch = new JObject { ["Details"] = patch };
        var later = db.Entry("later-array-update", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(2), patch);
        db.History.Add(later);
        var laterJson = later.Data.ToString();
        // Establish that the retained patch replays correctly before changing the baseline.
        JToken.DeepEquals(ChangeTrackingHelper.StripBaseProperties(ChangeTrackingHelper.ReassembleEntity(db.History)), expected).Should().BeTrue();

        var result = (await db.Rebase(Harness.Start.AddMinutes(1))).Single();

        result.Success.Should().BeTrue();
        var baseline = db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        JToken.DeepEquals(baseline.Data, new JObject(atCutoff) { ["createdOn"] = Harness.Start }).Should().BeTrue();
        JToken.DeepEquals(ChangeTrackingHelper.StripBaseProperties(ChangeTrackingHelper.ReassembleEntity(db.History)), expected).Should().BeTrue();
        db.History.Should().Contain(later);
        later.Data.ToString().Should().Be(laterJson);
        result.ArchivedTrackingEntries.Should().NotContain(later);
    }

    [Fact]
    public async Task Recovery_from_the_stored_entity_preserves_nulls_in_the_baseline()
    {
        var stored = NullSnapshot("nested");
        stored["id"] = "entity-1";
        stored["entityType"] = "Thing";
        stored["lastUpdated"] = Harness.Start.AddMinutes(2);
        var original = stored.ToString();
        var expected = ChangeTrackingHelper.StripBaseProperties(stored);
        var db = new Harness(false) { MaterializedEntities = [stored] };
        db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Update).Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue();
        JToken.DeepEquals(db.History.Single().Data, new JObject(expected) { ["createdOn"] = Harness.Start }).Should().BeTrue();
        stored.ToString().Should().Be(original);
        db.MaterializedWrites.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Materialization_keeps_its_existing_null_normalization_by_default(bool skipSave)
    {
        var db = new Harness(false);
        db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init).Data = NullSnapshot("flat");
        var original = JArray.FromObject(db.History).ToString();

        var response = await db.Materialize(skipSave);

        response.ResultingEntity.ContainsKey("Optional").Should().BeFalse();
        JToken.DeepEquals(response.ResultingEntity["Items"], new JArray("a", "b")).Should().BeTrue();
        JArray.FromObject(db.History).ToString().Should().Be(original);
        db.MaterializedWrites.Should().HaveCount(skipSave ? 0 : 1);
    }

    [Fact]
    public async Task Source_rebase_compacts_only_the_prefix_before_the_first_failed_patch()
    {
        var db = new Harness(true);
        var validPrefix = db.History.ToList();
        var (failed, later) = AppendSourceFailure(db);
        var retainedJson = JArray.FromObject(new[] { failed, later }).ToString();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse("the requested cutoff was only partially reached");
        result.FailureReason.Should().Contain("Partial rebase");
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(1));
        result.ArchivedTrackingEntries.Should().BeEquivalentTo(validPrefix);
        var baseline = db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        baseline.Timestamp.Should().Be(Harness.Start.AddMinutes(1));
        baseline.Data.Value<int>("Value").Should().Be(1);
        baseline.Data.ContainsKey("ChangedBeforeFailure").Should().BeFalse();
        db.History.Should().HaveCount(3).And.Contain(failed).And.Contain(later);
        JArray.FromObject(new[] { failed, later }).ToString().Should().Be(retainedJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_rebase_retains_the_entire_timestamp_group_containing_the_failure(bool reverseInput)
    {
        var db = new Harness(true);
        var validPrefix = db.History.ToList();
        var (failed, later) = AppendSourceFailure(db);
        failed._ts = 2;
        var sameTime = db.Entry("same-time-valid", "entity-1", ChangeTrackingEntryTypes.Update, failed.Timestamp, JObject.Parse("{ 'Value': [1,2] }"));
        sameTime._ts = 1;
        db.History.Add(sameTime);
        if (reverseInput) db.History.Reverse();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(1));
        result.ArchivedTrackingEntries.Should().BeEquivalentTo(validPrefix);
        db.History.Should().Contain(sameTime).And.Contain(failed).And.Contain(later);
        db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init).Data.Value<int>("Value").Should().Be(1);
    }

    [Fact]
    public async Task Failure_at_the_init_timestamp_preserves_the_entire_source_history()
    {
        var db = new Harness(true);
        var (failed, _) = AppendSourceFailure(db);
        failed.Timestamp = Harness.Start;
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("retained");
        result.RebasedTo.Should().BeNull();
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Equal(original);
        db.CreateCalls.Should().Be(0);
        db.DeleteCalls.Should().Be(0);
    }

    [Fact]
    public async Task Source_rebase_reuses_an_existing_safe_baseline_when_the_first_update_fails()
    {
        var db = new Harness(true);
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Update);
        var baseline = db.History.Single();
        var (failed, _) = AppendSourceFailure(db);
        var original = db.History.ToList();

        foreach (var attempt in Enumerable.Range(0, 2))
        {
            var result = (await db.Rebase()).Single();
            result.Success.Should().BeFalse();
            result.RebasedTo.Should().Be(baseline.Timestamp);
            result.ArchivedTrackingEntries.Should().BeEmpty();
        }

        db.History.Should().Equal(original).And.Contain(failed);
        db.CreateCalls.Should().Be(0);
        db.DeleteCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public async Task Source_rebase_respects_the_requested_cutoff_when_later_history_contains_a_failure(int cutoffMinutes, bool expectedSuccess)
    {
        var db = new Harness(true);
        var (failed, later) = AppendSourceFailure(db);

        var result = (await db.Rebase(Harness.Start.AddMinutes(cutoffMinutes))).Single();

        result.Success.Should().Be(expectedSuccess);
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(1));
        db.History.Should().Contain(failed).And.Contain(later);
        result.ArchivedTrackingEntries.Should().OnlyContain(entry => entry.Timestamp < failed.Timestamp);
    }

    [Fact]
    public async Task Partial_source_rebase_does_not_prevent_other_entities_from_rebasing()
    {
        var db = new Harness(true);
        var (failed, later) = AppendSourceFailure(db);
        db.Seed("entity-2");

        var results = await db.Rebase();

        results.Single(result => result.EntityId == "entity-1").Success.Should().BeFalse();
        results.Single(result => result.EntityId == "entity-2").Success.Should().BeTrue();
        db.History.Should().Contain(failed).And.Contain(later);
        db.History.Where(entry => entry.EntityId == "entity-2").Should().ContainSingle(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
    }

    [Fact]
    public async Task Failed_partial_baseline_write_preserves_the_whole_source_history()
    {
        var db = new Harness(true) { FailCreateFor = "entity-1" };
        AppendSourceFailure(db);
        var original = db.History.ToList();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("injected baseline failure");
        result.RebasedTo.Should().BeNull();
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Equal(original);
        db.DeleteCalls.Should().Be(0);
    }

    [Fact]
    public async Task Partial_rebase_preserves_deletion_failure_details_and_the_retained_tail()
    {
        var db = new Harness(true) { FailDeleteId = "entity-1-update" };
        var (failed, later) = AppendSourceFailure(db);
        var originalInit = db.History.Single(entry => entry.id == "entity-1-init");

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("injected delete failure");
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Contain(failed).And.Contain(later).And.Contain(entry => entry.id == "entity-1-update");
        db.History.Should().Contain(originalInit);
        db.History.Should().NotContain(entry => entry.id.StartsWith("replacement-"));
    }

    [Theory]
    [InlineData("client")]
    [InlineData("cli")]
    public async Task Public_source_rebase_results_report_the_actual_partial_cutoff_and_archived_prefix(string surface)
    {
        var db = new Harness(true);
        var prefix = db.History.ToList();
        var (failed, later) = AppendSourceFailure(db);

        var result = (await db.Rebase(Harness.Start.AddMinutes(3), surface)).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("Partial rebase");
        result.DataSource.Should().Be("SRC1");
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(1));
        result.ArchivedTrackingEntries.Should().BeEquivalentTo(prefix);
        db.History.Should().Contain(failed).And.Contain(later);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task Source_rebase_finds_the_safe_prefix_for_failures_at_different_positions(int failedMinute)
    {
        var db = new Harness(true);
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Update);
        for (var minute = 1; minute <= 8; minute++)
        {
            var patch = minute == failedMinute
                ? JObject.Parse("{ 'Value': [0,1,2,3,4] }")
                : new JObject { ["Value"] = new JArray(minute - 1, minute) };
            db.History.Add(db.Entry($"update-{minute}", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(minute), patch));
        }
        var failedTimestamp = Harness.Start.AddMinutes(failedMinute);
        var retained = db.History.Where(entry => entry.Timestamp >= failedTimestamp).ToList();
        var retainedJson = JArray.FromObject(retained).ToString();

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(failedMinute - 1));
        result.ArchivedTrackingEntries.Should().HaveCount(failedMinute == 1 ? 0 : failedMinute)
            .And.NotContain(entry => entry.Timestamp >= failedTimestamp);
        var baseline = db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        baseline.Data.Value<int>("Value").Should().Be(failedMinute - 1);
        db.History.Where(entry => entry.EntryType == ChangeTrackingEntryTypes.Update).Should().Equal(retained);
        JArray.FromObject(retained).ToString().Should().Be(retainedJson);
    }

    [Fact]
    public async Task Invalid_source_init_is_preserved_without_preventing_other_entities_from_rebasing()
    {
        var db = new Harness(true);
        db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init).Data = null!;
        var original = db.History.ToList();
        db.Seed("entity-2");

        var results = await db.Rebase();

        var failure = results.Single(result => result.EntityId == "entity-1");
        failure.Success.Should().BeFalse();
        failure.RebasedTo.Should().BeNull();
        failure.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Where(entry => entry.EntityId == "entity-1").Should().Equal(original);
        results.Single(result => result.EntityId == "entity-2").Success.Should().BeTrue();
    }

    [Theory]
    [InlineData("client")]
    [InlineData("cli")]
    public async Task Public_source_rebase_reports_no_checkpoint_when_the_first_timestamp_group_fails(string surface)
    {
        var db = new Harness(true);
        var (failed, _) = AppendSourceFailure(db);
        failed.Timestamp = Harness.Start;
        var original = db.History.ToList();

        var result = (await db.Rebase(Harness.Start.AddMinutes(3), surface)).Single();

        result.Success.Should().BeFalse();
        result.RebasedTo.Should().BeNull();
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().Equal(original);
        db.CreateCalls.Should().Be(0);
        db.DeleteCalls.Should().Be(0);
    }

    private static (ChangeTrackingEntry Failed, ChangeTrackingEntry Later) AppendSourceFailure(Harness db)
    {
        var failed = db.Entry("failed-update", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(2),
            JObject.Parse("{ 'ChangedBeforeFailure': [true], 'Value': [0,1,2,3,4] }"));
        var later = db.Entry("after-failure", "entity-1", ChangeTrackingEntryTypes.Update, Harness.Start.AddMinutes(3), JObject.Parse("{ 'Value': [1,3] }"));
        db.History.Add(failed);
        db.History.Add(later);
        return (failed, later);
    }

    private static JObject NullSnapshot(string shape) => JObject.Parse(shape switch
    {
        "flat" => "{ 'Value': 0, 'Optional': null, 'Items': ['a', null, 'b'], 'Empty': [] }",
        "nested" => "{ 'Value': 0, 'Details': { 'Optional': null, 'Items': [null, { 'Name': null, 'Children': [null, 'x', null] }, ['a', null, 'b'], null] } }",
        "all-null" => "{ 'Value': 0, 'Items': [null, null], 'Nested': [[null], [], [null, null]] }",
        _ => throw new ArgumentOutOfRangeException(nameof(shape))
    });

    internal sealed class Harness
    {
        public static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        private readonly bool source;
        private readonly IMediator mediator;
        private readonly Reimaginate.ProcessingLockService.IProcessingLockService locks;
        private readonly IIdService ids = Substitute.For<IIdService>();
        private readonly ITimeService time = Substitute.For<ITimeService>();
        private int nextId;
        private CancellationTokenSource? activeCancellation;
        public List<ChangeTrackingEntry> History { get; } = [];
        public List<ChangeTrackingEntry> Deleted { get; } = [];
        public List<JObject> MaterializedWrites { get; } = [];
        public List<JObject>? MaterializedEntities { get; init; }
        public string? FailCreateFor { get; init; }
        public string? FailDeleteId { get; init; }
        public bool OmitCreateConfirmation { get; init; }
        public bool OmitDeleteConfirmation { get; init; }
        public bool ThrowOnCreate { get; init; }
        public bool ThrowOnDelete { get; init; }
        public bool CancelAfterCreate { get; init; }
        public bool WrongCreateConfirmation { get; init; }
        public Func<CommitRebaseCheckpointCommand, CommitRebaseCheckpointResponse?>? RejectCheckpoint { get; set; }
        public int CheckpointCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public int MaterializeCalls { get; private set; }

        public Func<IRequest, CancellationToken, Task>? BeforeRequest { get; set; }

        public Harness(bool source, Reimaginate.ProcessingLockService.IProcessingLockService? lockService = null)
        {
            this.source = source;
            locks = lockService ?? new Reimaginate.ProcessingLockService.ProcessingLockService();
            mediator = new LocalMediator(Respond);
            ids.NewId<ChangeTrackingEntry>().Returns(_ => $"replacement-{Interlocked.Increment(ref nextId)}");
            time.Now().Returns(Start.AddHours(1));
            Seed("entity-1");
        }

        public ChangeTrackingEntry Entry(string id, string entityId, string type, DateTimeOffset timestamp, JObject data)
            => new() { id = id, DataSource = source ? "SRC1" : DataSources.DataHub, EntityType = "Thing", EntityId = entityId, EntryType = type, Timestamp = timestamp, Data = data };

        public void Seed(string entityId)
        {
            History.Add(Entry($"{entityId}-init", entityId, ChangeTrackingEntryTypes.Init, Start, new JObject { ["Value"] = 0 }));
            History.Add(Entry($"{entityId}-update", entityId, ChangeTrackingEntryTypes.Update, Start.AddMinutes(1), JObject.Parse("{ 'Value': [0,1] }")));
        }

        public Task<List<RebaseEntityTrackingResult>> Rebase(DateTimeOffset? rebaseTo = null, string surface = "internal", bool includeArchivedTrackingEntries = true, string sourceEntityType = "Thing", List<string>? requestedEntityIds = null, string sourceDataSource = "SRC1") =>
            RebaseWithCancellation(TestContext.Current.CancellationToken, rebaseTo, surface, includeArchivedTrackingEntries, sourceEntityType, requestedEntityIds, sourceDataSource);

        public async Task<List<RebaseEntityTrackingResult>> RebaseWithCancellation(CancellationToken cancellationToken, DateTimeOffset? rebaseTo = null, string surface = "internal", bool includeArchivedTrackingEntries = true, string sourceEntityType = "Thing", List<string>? requestedEntityIds = null, string sourceDataSource = "SRC1")
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, cancellationToken);
            activeCancellation = cancellation;
            var entityIds = requestedEntityIds ?? History.Select(e => e.EntityId).Distinct().ToList();
            if (source)
            {
                if (surface == "client")
                    return (await new Reimaginate.DataHub.Requests.External.Client.RebaseSourceEntities.RebaseSourceEntitiesRequestHandler(mediator).HandleAsync(new()
                    {
                        DataSource = sourceDataSource, EntityType = sourceEntityType, EntityIds = entityIds, IncludeArchivedTrackingEntries = includeArchivedTrackingEntries, RebaseTo = rebaseTo
                    }, cancellation.Token)).Results;
                if (surface == "cli")
                    return (await new Reimaginate.DataHub.Requests.External.CLI.RebaseSourceEntities.RebaseSourceEntitiesRequestHandler(mediator).HandleAsync(new()
                    {
                        DataSource = sourceDataSource, EntityType = sourceEntityType, EntityIds = entityIds, IncludeArchivedTrackingEntries = includeArchivedTrackingEntries, RebaseTo = rebaseTo
                    }, cancellation.Token)).Results;
                return (await new ProcessRebaseSourceEntitiesRequestHandler(mediator, locks).HandleAsync(new() { DataSource = sourceDataSource, EntityType = sourceEntityType, EntityIds = entityIds, IncludeArchivedTrackingEntries = includeArchivedTrackingEntries, RebaseTo = rebaseTo }, cancellation.Token)).Results;
            }
            if (surface == "client")
                return (await new Reimaginate.DataHub.Requests.External.Client.RebaseDataHubEntities.RebaseDataHubEntitiesRequestHandler(mediator).HandleAsync(new()
                {
                    EntityType = "Thing", EntityIds = entityIds, IncludeArchivedTrackingEntries = includeArchivedTrackingEntries, RebaseTo = rebaseTo
                }, cancellation.Token)).Results;
            if (surface == "cli")
                return (await new Reimaginate.DataHub.Requests.External.CLI.RebaseDataHubEntities.RebaseDataHubEntitiesRequestHandler(mediator).HandleAsync(new()
                {
                    EntityType = "Thing", EntityIds = entityIds, IncludeArchivedTrackingEntries = includeArchivedTrackingEntries, RebaseTo = rebaseTo
                }, cancellation.Token)).Results;
            return (await new ProcessRebaseDataHubEntitiesRequestHandler(mediator, locks).HandleAsync(new() { EntityType = "Thing", EntityIds = entityIds, IncludeArchivedTrackingEntries = includeArchivedTrackingEntries, RebaseTo = rebaseTo }, cancellation.Token)).Results;
        }

        public Task<MaterializeDataHubEntityResponse> Materialize(bool skipSave) =>
            new MaterializeDataHubEntityRequestHandler(mediator).HandleAsync(new()
            {
                EntityType = "Thing",
                EntityId = "entity-1",
                TrackingEntries = History.ToList(),
                SkipSave = skipSave
            }, TestContext.Current.CancellationToken);

        private async Task<object> Respond(IRequest request, CancellationToken cancellationToken)
        {
            if (BeforeRequest != null) await BeforeRequest(request, cancellationToken);
            switch (request)
            {
                case ProcessRebaseDataHubEntitiesRequest rebase:
                    return await new ProcessRebaseDataHubEntitiesRequestHandler(mediator, locks).HandleAsync(rebase, cancellationToken);
                case ProcessRebaseSourceEntitiesRequest rebase:
                    return await new ProcessRebaseSourceEntitiesRequestHandler(mediator, locks).HandleAsync(rebase, cancellationToken);
                case GetSourceRebaseIdentitiesQuery discover:
                    return SourceRebaseIdentityTests.IdentityPairs(History, discover);
                case GetAllTrackingEntriesForEntitiesRequest read:
                    return new GetAllTrackingEntriesForEntitiesResponse { TrackingEntries = History.Where(entry => read.EntityIds.Contains(entry.EntityId) && entry.DataSource == read.DataSource && entry.EntityType == read.EntityType).ToList() };
                case GetMaterializedEntitiesByIdRequest read:
                    return new GetMaterializedEntitiesByIdResponse { Results = MaterializedEntities ?? History.Where(entry => read.EntityIds.Contains(entry.EntityId)).GroupBy(e => e.EntityId).Select(g => ChangeTrackingHelper.ReassembleEntity(g.ToList())).ToList() };
                case MaterializeDataHubEntityRequest materialize:
                    MaterializeCalls++;
                    return await new MaterializeDataHubEntityRequestHandler(mediator).HandleAsync(materialize, cancellationToken);
                case GetTrackedEntityRequest tracked:
                    return (await new GetTrackedEntityRequestHandler(mediator).HandleAsync(tracked, cancellationToken))!;
                case UpsertDataHubEntitiesCommand upsert:
                    MaterializedWrites.AddRange(upsert.Entities);
                    return new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] };
                case CommitRebaseCheckpointCommand checkpoint:
                {
                    lock (History)
                    {
                        CheckpointCalls++;
                        checkpoint.EnsureLease?.Invoke();
                        cancellationToken.ThrowIfCancellationRequested();
                        if (RejectCheckpoint?.Invoke(checkpoint) is { } rejected) return rejected;
                        if (checkpoint.Baseline != null)
                        {
                            CreateCalls++;
                            if (ThrowOnCreate) throw new Exception("injected write exception");
                            if (checkpoint.Baseline.EntityId == FailCreateFor)
                                return new CommitRebaseCheckpointResponse { FailureReason = "injected baseline failure" };
                            if (OmitCreateConfirmation || WrongCreateConfirmation)
                                return new CommitRebaseCheckpointResponse { FailureReason = "Atomic checkpoint was not confirmed" };
                            if (CancelAfterCreate)
                            {
                                activeCancellation!.Cancel();
                                throw new OperationCanceledException(cancellationToken);
                            }
                        }
                        if (checkpoint.EntriesToDelete.Count > 0)
                        {
                            DeleteCalls++;
                            if (ThrowOnDelete) throw new Exception("injected delete exception");
                            if (checkpoint.EntriesToDelete.Any(entry => entry.id == FailDeleteId))
                                return new CommitRebaseCheckpointResponse { FailureReason = "injected delete failure" };
                            if (OmitDeleteConfirmation)
                                return new CommitRebaseCheckpointResponse { FailureReason = "Atomic checkpoint was not confirmed" };
                        }
                        (checkpoint.EntriesToDelete.Count + (checkpoint.Baseline == null ? 0 : 1)).Should().BeLessThanOrEqualTo(100);
                        var deletingIds = checkpoint.EntriesToDelete.Select(entry => entry.id).ToHashSet();
                        if (checkpoint.Baseline == null)
                            History.Should().Contain(entry => entry.EntryType == ChangeTrackingEntryTypes.Init && !deletingIds.Contains(entry.id) &&
                                entry.EntityId == checkpoint.EntriesToDelete[0].EntityId);
                        History.RemoveAll(entry => deletingIds.Contains(entry.id));
                        Deleted.AddRange(checkpoint.EntriesToDelete);
                        if (checkpoint.Baseline != null) History.Add(checkpoint.Baseline);
                        return new CommitRebaseCheckpointResponse { Success = true };
                    }
                }
                case InitTrackedEntitiesRequest init:
                    return await new InitTrackedEntitiesRequestHandler(ids, mediator, time).HandleAsync(init, cancellationToken);
                case CreateCosmosDocumentsCommand<ChangeTrackingEntry> create:
                {
                    CreateCalls++;
                    if (ThrowOnCreate) throw new Exception("injected write exception");
                    var response = new CreateCosmosDocumentsResponse<ChangeTrackingEntry> { Successes = [], Failures = [] };
                    foreach (var entry in create.Documents)
                    {
                        if (entry.EntityId == FailCreateFor)
                            response.Failures.Add(new DataAccessFailure<ChangeTrackingEntry>(entry, new Exception("injected baseline failure")));
                        else if (!OmitCreateConfirmation)
                        {
                            History.Add(entry);
                            response.Successes.Add(WrongCreateConfirmation
                                ? new ChangeTrackingEntry { id = entry.id, DataSource = "OTHER", EntityType = entry.EntityType, EntityId = entry.EntityId, EntryType = entry.EntryType, Timestamp = entry.Timestamp, Data = entry.Data }
                                : entry);
                        }
                    }
                    if (CancelAfterCreate) activeCancellation!.Cancel();
                    return response;
                }
                case DeleteCosmosDocumentsCommand<ChangeTrackingEntry> delete:
                {
                    DeleteCalls++;
                    if (ThrowOnDelete) throw new Exception("injected delete exception");
                    var response = new DeleteCosmosDocumentsResponse<ChangeTrackingEntry> { Successes = [], Failures = [] };
                    foreach (var entry in delete.Documents)
                    {
                        // Enforce the safety invariant at the persistence boundary, before any deletion.
                        History.Should().Contain(e => e.EntityId == entry.EntityId && e.EntryType == ChangeTrackingEntryTypes.Init && !delete.Documents.Contains(e));
                        if (entry.id == FailDeleteId)
                            response.Failures.Add(new DataAccessFailure<ChangeTrackingEntry>(entry, new Exception("injected delete failure")));
                        else if (!OmitDeleteConfirmation)
                        {
                            History.Remove(entry);
                            Deleted.Add(entry);
                            response.Successes.Add(entry);
                        }
                    }
                    return response;
                }
                default:
                    throw new NotSupportedException(request.GetType().Name);
            }
        }
    }

    private sealed class LocalMediator(Func<IRequest, CancellationToken, Task<object>> respond) : IMediator
    {
        public async Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            try { return (TResponse)await respond(request, cancellationToken); }
            catch (Exception ex) { return ex; }
        }
        public async Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
        {
            try { return await respond(request, cancellationToken); }
            catch (Exception ex) { return ex; }
        }
        public async Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            try { return ((TResponse)await respond(request, cancellationToken), null); }
            catch (Exception ex) { exceptionHandler?.Invoke(ex); return (default, ex); }
        }
        public Task<object> SendAndHandleExceptions<TRequest>(TRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<TResponse> SendAndHandleExceptions<TResponse>(IRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
            => throw new NotSupportedException();
    }
}
