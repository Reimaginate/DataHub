using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Xunit;
using Harness = Reimaginate.DataHub.Test.Unit.Tests.RebaseTrackingPersistenceTests.Harness;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class RebaseCreationDateTests
{
    private static readonly DateTimeOffset ExplicitCreatedOn = DateTimeOffset.Parse("2025-06-01T09:15:00+10:00");

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Rebase_preserves_creation_date_when_a_later_update_is_materialized(bool source, bool explicitDate)
    {
        var db = new Harness(source);
        if (explicitDate) db.History[0].Data["createdOn"] = ExplicitCreatedOn;
        var original = JArray.FromObject(db.History).ToString();
        var expectedDate = explicitDate ? ExplicitCreatedOn : Harness.Start;

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(1));
        JArray.FromObject(result.ArchivedTrackingEntries).ToString().Should().Be(original);
        db.History.Single().Data.DateTimeOffsetValueRequired("createdOn").Should().Be(expectedDate);
        db.MaterializedWrites.Should().BeEmpty();

        db.History.Add(db.Entry("later-update", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start.AddMinutes(2), JObject.Parse("{ 'Value': [1,2] }")));
        var materialized = await db.Materialize(skipSave: false);

        materialized.ResultingEntity.DateTimeOffsetValueRequired("createdOn").Should().Be(expectedDate);
        materialized.ResultingEntity.DateTimeOffsetValueRequired("lastUpdated").Should().Be(Harness.Start.AddMinutes(2));
        materialized.ResultingEntity.Value<int>("Value").Should().Be(2);
        db.MaterializedWrites.Single().DateTimeOffsetValueRequired("createdOn").Should().Be(expectedDate);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Intermediate_checkpoints_and_retry_keep_the_creation_date(bool source, bool explicitDate)
    {
        var db = new Harness(source);
        if (explicitDate) db.History[0].Data["createdOn"] = ExplicitCreatedOn;
        var expectedDate = explicitDate ? ExplicitCreatedOn : Harness.Start;
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Update);
        for (var index = 1; index <= 250; index++)
            db.History.Add(db.Entry($"update-{index}", "entity-1", ChangeTrackingEntryTypes.Update,
                Harness.Start.AddMinutes(index), new JObject { ["Value"] = new JArray(index - 1, index) }));
        var checkpointDates = new List<DateTimeOffset>();
        var rejectLater = true;
        db.RejectCheckpoint = checkpoint =>
        {
            if (rejectLater && checkpoint.Baseline.Timestamp > Harness.Start.AddMinutes(98))
                return new() { FailureReason = "injected later checkpoint failure" };
            checkpointDates.Add(checkpoint.Baseline.Data.DateTimeOffsetValueRequired("createdOn"));
            return null;
        };

        var partial = (await db.Rebase()).Single();

        partial.Success.Should().BeFalse();
        partial.RebasedTo.Should().Be(Harness.Start.AddMinutes(98));
        checkpointDates.Should().Equal(expectedDate);
        ChangeTrackingHelper.ReassembleEntity(db.History).DateTimeOffsetValueRequired("createdOn").Should().Be(expectedDate);

        rejectLater = false;
        var retry = (await db.Rebase()).Single();

        retry.Success.Should().BeTrue(retry.FailureReason);
        checkpointDates.Should().HaveCount(3).And.OnlyContain(date => date == expectedDate);
        db.History.Should().ContainSingle();
        var materialized = await db.Materialize(skipSave: false);
        materialized.ResultingEntity.DateTimeOffsetValueRequired("createdOn").Should().Be(expectedDate);
        materialized.ResultingEntity.Value<int>("Value").Should().Be(250);
    }

    [Theory]
    [InlineData("stored")]
    [InlineData("baseline")]
    [InlineData("baseline-timestamp")]
    public async Task Recovery_uses_the_known_creation_date_without_mutating_the_stored_entity(string dateSource)
    {
        var stored = new JObject
        {
            ["id"] = "entity-1", ["entityType"] = "Thing", ["Value"] = 1,
            ["lastUpdated"] = Harness.Start.AddMinutes(1)
        };
        if (dateSource == "stored") stored["createdOn"] = ExplicitCreatedOn;
        var original = stored.ToString();
        var db = new Harness(false) { MaterializedEntities = [stored] };
        if (dateSource == "baseline") db.History[0].Data["createdOn"] = ExplicitCreatedOn;
        db.History[1].Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");
        var expectedDate = dateSource == "baseline-timestamp" ? Harness.Start : ExplicitCreatedOn;

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        db.History.Single().Data.DateTimeOffsetValueRequired("createdOn").Should().Be(expectedDate);
        (await db.Materialize(skipSave: false)).ResultingEntity.DateTimeOffsetValueRequired("createdOn").Should().Be(expectedDate);
        stored.ToString().Should().Be(original);
    }

    [Fact]
    public async Task Long_recovery_uses_the_stored_creation_date()
    {
        var stored = new JObject
        {
            ["id"] = "entity-1", ["entityType"] = "Thing", ["Value"] = 250,
            ["createdOn"] = ExplicitCreatedOn, ["lastUpdated"] = Harness.Start.AddMinutes(250)
        };
        var db = new Harness(false) { MaterializedEntities = [stored] };
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Update);
        for (var index = 1; index <= 250; index++)
            db.History.Add(db.Entry($"update-{index}", "entity-1", ChangeTrackingEntryTypes.Update,
                Harness.Start.AddMinutes(index), new JObject { ["Value"] = new JArray(index - 1, index) }));
        db.History.Single(entry => entry.id == "update-121").Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().NotBeNullOrWhiteSpace();
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(250));
        var baseline = db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        baseline.Data.DateTimeOffsetValueRequired("createdOn").Should().Be(ExplicitCreatedOn);
        baseline.Data.Value<int>("Value").Should().Be(250);
    }

    [Fact]
    public async Task Partial_source_rebase_keeps_creation_date_with_the_failed_entry_retained()
    {
        var db = new Harness(true);
        db.History[0].Data["createdOn"] = ExplicitCreatedOn;
        var failed = db.Entry("invalid-update", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start.AddMinutes(2), JObject.Parse("{ 'Value': [0,1,2,3,4] }"));
        db.History.Add(failed);

        var result = (await db.Rebase()).Single();

        result.Success.Should().BeFalse();
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(1));
        db.History.Should().Contain(failed);
        var baseline = db.History.Single(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        baseline.Data.DateTimeOffsetValueRequired("createdOn").Should().Be(ExplicitCreatedOn);
        ChangeTrackingHelper.ReassembleEntity([baseline]).Value<int>("Value").Should().Be(1);
    }
}
