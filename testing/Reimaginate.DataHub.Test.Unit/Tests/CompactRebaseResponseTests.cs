using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;
using Xunit;
using ClientRequests = Reimaginate.DataHub.SharedModels.Requests.Client;
using CliRequests = Reimaginate.DataHub.SharedModels.Requests.CLI;
using Harness = Reimaginate.DataHub.Test.Unit.Tests.RebaseTrackingPersistenceTests.Harness;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class CompactRebaseResponseTests
{
    [Theory]
    [InlineData(false, "internal", false)]
    [InlineData(true, "internal", false)]
    [InlineData(false, "client", false)]
    [InlineData(true, "client", false)]
    [InlineData(false, "cli", false)]
    [InlineData(true, "cli", false)]
    [InlineData(false, "internal", true)]
    [InlineData(true, "internal", true)]
    [InlineData(false, "client", true)]
    [InlineData(true, "client", true)]
    [InlineData(false, "cli", true)]
    [InlineData(true, "cli", true)]
    public async Task Responses_preserve_status_cutoff_and_count_with_optional_payloads(bool source, string surface, bool include)
    {
        var db = new Harness(source);
        db.History[0].Data["PrivateHistoryMarker"] = "large archived payload";
        var original = db.History.ToList();

        var result = (await db.Rebase(surface: surface, includeArchivedTrackingEntries: include)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(1));
        result.ArchivedTrackingEntryCount.Should().Be(2);
        result.Warning.Should().BeNull();
        if (include) result.ArchivedTrackingEntries.Should().BeEquivalentTo(original);
        else
        {
            result.ArchivedTrackingEntries.Should().BeEmpty();
            JsonConvert.SerializeObject(result).Should().NotContain("PrivateHistoryMarker");
        }
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(1);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Counts_include_only_confirmed_original_entries_after_a_partial_checkpoint_failure(bool source, bool include)
    {
        var db = LongHistory(source);
        db.RejectCheckpoint = checkpoint => checkpoint.Baseline.Timestamp > Harness.Start.AddMinutes(98)
            ? new() { FailureReason = "later checkpoint rejected" } : null;

        var result = (await db.Rebase(includeArchivedTrackingEntries: include)).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("later checkpoint rejected");
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(98));
        result.ArchivedTrackingEntryCount.Should().Be(99);
        result.ArchivedTrackingEntries.Should().HaveCount(include ? 99 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Long_rebase_excludes_intermediate_baselines_from_the_count(bool source)
    {
        var db = LongHistory(source);

        var result = (await db.Rebase(includeArchivedTrackingEntries: false)).Single();

        result.Success.Should().BeTrue();
        db.CheckpointCalls.Should().Be(3);
        result.ArchivedTrackingEntryCount.Should().Be(251);
        result.ArchivedTrackingEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Compact_persistence_never_accumulates_confirmed_payloads_between_checkpoints()
    {
        var db = LongHistory(false);
        var result = new RebaseEntityTrackingResult();
        var initial = db.History[0];
        var plan = new RebaseTrackingPlan(new InitTrackedEntityRequest
        {
            DataSource = initial.DataSource, EntityType = initial.EntityType, EntityId = initial.EntityId,
            Timestamp = Harness.Start.AddMinutes(250), EntityData = new JObject { ["Value"] = 250, ["createdOn"] = Harness.Start }
        }, db.History.ToList(), result, IncludeArchivedTrackingEntries: false);
        var countsBeforeCommit = new List<int>();
        var mediator = Substitute.For<IMediator>();
        mediator.TrySend(Arg.Any<CommitRebaseCheckpointCommand>(), Arg.Any<CancellationToken>(), Arg.Any<Action<Exception>?>())
            .Returns(_ =>
            {
                result.ArchivedTrackingEntries.Should().BeEmpty();
                countsBeforeCommit.Add(result.ArchivedTrackingEntryCount);
                return Task.FromResult<(CommitRebaseCheckpointResponse?, Exception?)>((new() { Success = true }, null));
            });

        await RebaseTrackingPersistence.PersistAsync(mediator, [plan], TestContext.Current.CancellationToken);

        countsBeforeCommit.Should().Equal(0, 99, 197);
        result.ArchivedTrackingEntryCount.Should().Be(251);
        result.ArchivedTrackingEntries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compact_cleanup_counts_entries_older_than_a_retained_baseline(bool source)
    {
        var db = LongHistory(source);
        var retained = db.Entry("retained-init", "entity-1", ChangeTrackingEntryTypes.Init,
            Harness.Start.AddMinutes(251), new JObject { ["Value"] = 250 });
        db.History.Add(retained);

        var result = (await db.Rebase(includeArchivedTrackingEntries: false)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.ArchivedTrackingEntryCount.Should().Be(251);
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().ContainSingle().Which.Should().BeSameAs(retained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_ops_and_unconfirmed_writes_have_zero_counts(bool source)
    {
        var db = new Harness(source);
        var noOp = (await db.Rebase(Harness.Start.AddTicks(-1), includeArchivedTrackingEntries: false)).Single();
        noOp.Success.Should().BeTrue();
        noOp.ArchivedTrackingEntryCount.Should().Be(0);
        noOp.ArchivedTrackingEntries.Should().BeEmpty();
        db.RejectCheckpoint = _ => new() { FailureReason = "unconfirmed", OutcomeUnknown = true };

        var failed = (await db.Rebase(includeArchivedTrackingEntries: false)).Single();

        failed.Success.Should().BeFalse();
        failed.ArchivedTrackingEntryCount.Should().Be(0);
        failed.ArchivedTrackingEntries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Partial_source_rebase_reports_confirmed_count_without_losing_failure_details(bool include)
    {
        var db = new Harness(true);
        db.History.Add(db.Entry("bad-update", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start.AddMinutes(2), JObject.Parse("{ 'Value': [0,1,2,3,4] }")));

        var result = (await db.Rebase(surface: "client", includeArchivedTrackingEntries: include)).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("Partial rebase");
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(1));
        result.ArchivedTrackingEntryCount.Should().Be(2);
        result.ArchivedTrackingEntries.Should().HaveCount(include ? 2 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compact_stored_recovery_keeps_warning_and_count_on_success_or_cleanup_failure(bool failCleanup)
    {
        var db = LongHistory(false, recover: true);
        if (failCleanup) db.RejectCheckpoint = checkpoint => checkpoint.Baseline == null ? new() { FailureReason = "cleanup failed" } : null;

        var result = (await db.Rebase(surface: "cli", includeArchivedTrackingEntries: false)).Single();

        result.Success.Should().Be(!failCleanup);
        result.Warning.Should().Contain("STORED_ENTITY_RECOVERY");
        result.RebasedTo.Should().Be(Harness.Start.AddMinutes(250));
        result.ArchivedTrackingEntryCount.Should().Be(failCleanup ? 99 : 251);
        result.ArchivedTrackingEntries.Should().BeEmpty();
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(17);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compact_results_remain_payload_free_across_batches_of_500(bool source)
    {
        var db = new Harness(source);
        for (var i = 2; i <= 501; i++) db.Seed($"entity-{i}");

        var results = await db.Rebase(includeArchivedTrackingEntries: false);

        results.Should().HaveCount(501).And.OnlyContain(result => result.Success &&
            result.ArchivedTrackingEntryCount == 2 && result.ArchivedTrackingEntries.Count == 0);
    }

    [Theory]
    [InlineData(typeof(ClientRequests.RebaseSourceEntitiesRequest))]
    [InlineData(typeof(ClientRequests.RebaseDataHubEntitiesRequest))]
    [InlineData(typeof(CliRequests.RebaseSourceEntitiesRequest))]
    [InlineData(typeof(CliRequests.RebaseDataHubEntitiesRequest))]
    public void Older_payloads_keep_full_archived_entries_by_default(Type requestType)
    {
        var request = JsonConvert.DeserializeObject("{}", requestType)!;
        var property = requestType.GetProperty("IncludeArchivedTrackingEntries")!;

        property.GetValue(request).Should().Be(true);
        var compact = JsonConvert.DeserializeObject("{\"IncludeArchivedTrackingEntries\":false}", requestType)!;
        property.GetValue(compact).Should().Be(false);
    }

    private static Harness LongHistory(bool source, bool recover = false)
    {
        var db = new Harness(source)
        {
            MaterializedEntities = recover ? [new JObject
            {
                ["id"] = "entity-1", ["entityType"] = "Thing", ["Value"] = 17, ["lastUpdated"] = Harness.Start.AddMinutes(251)
            }] : null
        };
        db.History.RemoveAll(entry => entry.EntryType == ChangeTrackingEntryTypes.Update);
        for (var i = 1; i <= 250; i++) db.History.Add(db.Entry($"update-{i}", "entity-1", ChangeTrackingEntryTypes.Update,
            Harness.Start.AddMinutes(i), new JObject { ["Value"] = new JArray(i - 1, i) }));
        if (recover) db.History[121].Data = JObject.Parse("{ 'Value': [0,1,2,3,4] }");
        return db;
    }
}
