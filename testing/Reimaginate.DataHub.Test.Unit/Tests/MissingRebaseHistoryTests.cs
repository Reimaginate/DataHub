using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;
using Reimaginate.DataHub.SharedModels.Constants;
using Xunit;
using Harness = Reimaginate.DataHub.Test.Unit.Tests.RebaseTrackingPersistenceTests.Harness;
using LockService = Reimaginate.ProcessingLockService.ProcessingLockService;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class MissingRebaseHistoryTests
{
    private static DateTimeOffset Modified => Harness.Start.AddMinutes(10).ToOffset(TimeSpan.FromHours(10));

    [Theory]
    [InlineData("internal", false)]
    [InlineData("internal", true)]
    [InlineData("client", false)]
    [InlineData("client", true)]
    [InlineData("cli", false)]
    [InlineData("cli", true)]
    public async Task Missing_history_creates_one_locked_baseline_from_the_stored_snapshot_and_returns_a_warning(string surface, bool include)
    {
        using var locks = new LockService();
        var stored = Stored();
        var original = stored.ToString();
        var db = new Harness(false, locks) { MaterializedEntities = [stored] };
        db.History.Clear();
        db.BeforeRequest = async (request, ct) =>
        {
            if (request is ProcessRebaseDataHubEntitiesRequest) return;
            (await locks.GetLocksAsync(ct)).Result!.Select(item => item.Key).Should().Equal($"entities/{DataSources.DataHub}/Thing/entity-1");
            if (request is CommitRebaseCheckpointCommand checkpoint)
            {
                checkpoint.Baseline.Should().NotBeNull();
                checkpoint.EntriesToDelete.Should().BeEmpty();
            }
        };

        var result = (await db.Rebase(surface: surface, includeArchivedTrackingEntries: include, requestedEntityIds: ["entity-1"])).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.DataSource.Should().Be(DataSources.DataHub);
        result.Warning.Should().Contain("STORED_ENTITY_RECOVERY").And.Contain("no tracking history existed");
        result.RebasedTo.Should().Be(Modified);
        result.RebasedTo!.Value.Offset.Should().Be(Modified.Offset);
        result.ArchivedTrackingEntryCount.Should().Be(0);
        result.ArchivedTrackingEntries.Should().BeEmpty();
        var baseline = db.History.Should().ContainSingle().Subject;
        baseline.DataSource.Should().Be(DataSources.DataHub);
        baseline.EntityType.Should().Be("Thing");
        baseline.EntityId.Should().Be("entity-1");
        baseline.EntryType.Should().Be("Init");
        baseline.Timestamp.Should().Be(Modified);
        JToken.DeepEquals(baseline.Data, RebaseTrackingPersistence.CreateBaselineData(stored)).Should().BeTrue();
        ChangeTrackingHelper.ReassembleEntity(db.History).Value<int>("Value").Should().Be(17);
        db.CheckpointCalls.Should().Be(1);
        db.CreateCalls.Should().Be(1);
        db.DeleteCalls.Should().Be(0);
        db.MaterializedWrites.Should().BeEmpty();
        stored.ToString().Should().Be(original);
        (await locks.GetLocksAsync(TestContext.Current.CancellationToken)).Result.Should().BeEmpty();

        var retry = (await db.Rebase()).Single();
        retry.Success.Should().BeTrue(retry.FailureReason);
        retry.Warning.Should().BeNull();
        retry.RebasedTo.Should().Be(Modified);
        db.History.Should().ContainSingle().Which.Should().BeSameAs(baseline);
        db.CheckpointCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Historical_recovery_uses_the_stored_timestamp_when_it_precedes_or_equals_the_cutoff(int cutoffMinutesAfterStored)
    {
        var db = EmptyHistory(Stored());

        var result = (await db.Rebase(Modified.AddMinutes(cutoffMinutesAfterStored), requestedEntityIds: ["entity-1"])).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.Warning.Should().Contain("STORED_ENTITY_RECOVERY");
        result.RebasedTo.Should().Be(Modified);
        db.History.Should().ContainSingle().Which.Timestamp.Should().Be(Modified);
    }

    [Theory]
    [InlineData("missing", false)]
    [InlineData("null", false)]
    [InlineData("invalid", false)]
    [InlineData("missing", true)]
    [InlineData("null", true)]
    [InlineData("invalid", true)]
    [InlineData("newer", true)]
    public async Task Unknown_or_unsafe_stored_timestamp_fails_only_that_entity(string reason, bool historical)
    {
        var stored = Stored();
        if (reason == "missing") stored.Remove("lastUpdated");
        if (reason == "null") stored["lastUpdated"] = JValue.CreateNull();
        if (reason == "invalid") stored["lastUpdated"] = "not-a-date";
        var db = EmptyHistory(stored);
        db.Seed("entity-2");

        var results = await db.Rebase(historical ? Modified.AddMinutes(-1) : null, requestedEntityIds: ["entity-1", "entity-2"]);

        var failure = results.Single(result => result.EntityId == "entity-1");
        failure.Success.Should().BeFalse();
        failure.Warning.Should().BeNull();
        failure.RebasedTo.Should().BeNull();
        failure.ArchivedTrackingEntryCount.Should().Be(0);
        failure.ArchivedTrackingEntries.Should().BeEmpty();
        db.History.Should().NotContain(entry => entry.EntityId == "entity-1");
        results.Single(result => result.EntityId == "entity-2").Success.Should().BeTrue();
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("unknown")]
    [InlineData("too-large")]
    [InlineData("exception")]
    public async Task Unconfirmed_baseline_creation_never_reports_recovery_or_retries_the_write(string outcome)
    {
        var db = EmptyHistory(Stored());
        db.RejectCheckpoint = _ => outcome == "exception"
            ? throw new InvalidOperationException("write failed")
            : new() { FailureReason = "not confirmed", OutcomeUnknown = outcome == "unknown", TooLarge = outcome == "too-large" };

        var result = (await db.Rebase(requestedEntityIds: ["entity-1"])).Single();

        result.Success.Should().BeFalse();
        result.Warning.Should().BeNull();
        result.RebasedTo.Should().BeNull();
        result.ArchivedTrackingEntryCount.Should().Be(0);
        result.ArchivedTrackingEntries.Should().BeEmpty();
        db.CheckpointCalls.Should().Be(1);
        db.History.Should().BeEmpty();
        db.MaterializedWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancellation_before_the_create_only_checkpoint_releases_locks_without_creating_history()
    {
        using var locks = new LockService();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var db = new Harness(false, locks) { MaterializedEntities = [Stored()] };
        db.History.Clear();
        db.BeforeRequest = async (request, ct) =>
        {
            if (request is CommitRebaseCheckpointCommand)
            {
                cancellation.Cancel();
                // Wait until cancellation reaches the batch token before allowing
                // the checkpoint to run; cancellation propagation is asynchronous.
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
        };

        Func<Task> run = () => db.RebaseWithCancellation(cancellation.Token, requestedEntityIds: ["entity-1"]);

        await run.Should().ThrowAsync<OperationCanceledException>();
        db.History.Should().BeEmpty();
        db.CheckpointCalls.Should().Be(0);
        (await locks.GetLocksAsync(TestContext.Current.CancellationToken)).Result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_history_without_an_authoritative_stored_entity_returns_not_found(bool source)
    {
        var db = new Harness(source) { MaterializedEntities = [] };
        db.History.Clear();

        var result = (await db.Rebase(requestedEntityIds: ["entity-1"])).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("NOT_FOUND");
        result.Warning.Should().BeNull();
        db.CheckpointCalls.Should().Be(0);
        db.History.Should().BeEmpty();
    }

    private static Harness EmptyHistory(JObject stored)
    {
        var db = new Harness(false) { MaterializedEntities = [stored] };
        db.History.Clear();
        return db;
    }

    private static JObject Stored() => new()
    {
        ["id"] = "entity-1", ["entityType"] = "Thing", ["Value"] = 17,
        ["Items"] = new JArray("A", JValue.CreateNull(), "B"), ["Optional"] = JValue.CreateNull(),
        ["createdOn"] = Harness.Start.AddDays(-1), ["lastUpdated"] = Modified
    };
}
