using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Reimaginate.DataHub.DataAccess.Queries.GetSourceRebaseIdentities;
using Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;
using Harness = Reimaginate.DataHub.Test.Unit.Tests.RebaseTrackingPersistenceTests.Harness;
using LockService = Reimaginate.ProcessingLockService.ProcessingLockService;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class SourceRebaseIdentityTests
{
    [Theory]
    [InlineData("internal", "SRC1", "thing")]
    [InlineData("cli", "SRC1", "thing")]
    [InlineData("client", "SRC1", "thing")]
    [InlineData("internal", "src1", "thing")]
    [InlineData("cli", "src1", "thing")]
    [InlineData("client", "src1", "thing")]
    [InlineData("internal", "src1", "Thing")]
    [InlineData("cli", "src1", "Thing")]
    [InlineData("client", "src1", "Thing")]
    public async Task Case_insensitive_requests_use_the_exact_stored_pair_for_locks_reads_writes_and_results(string surface, string dataSource, string entityType)
    {
        using var locks = new LockService();
        var db = new Harness(true, locks);
        var historyReads = 0;
        db.BeforeRequest = async (request, ct) =>
        {
            if (request is GetSourceRebaseIdentitiesQuery or Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities.ProcessRebaseSourceEntitiesRequest) return;
            (await locks.GetLocksAsync(ct)).Result!.Select(item => item.Key).Should().Equal("entities/SRC1/Thing/entity-1");
            if (request is GetAllTrackingEntriesForEntitiesRequest read)
            {
                historyReads++;
                read.DataSource.Should().Be("SRC1");
                read.EntityType.Should().Be("Thing");
            }
        };

        var result = (await db.Rebase(surface: surface, sourceEntityType: entityType, sourceDataSource: dataSource)).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.DataSource.Should().Be("SRC1");
        result.EntityType.Should().Be("Thing");
        result.ArchivedTrackingEntries.Should().HaveCount(2).And.OnlyContain(entry => entry.EntityType == "Thing");
        historyReads.Should().Be(1);
        db.History.Should().ContainSingle().Which.EntityType.Should().Be("Thing");
        db.History.Single().DataSource.Should().Be("SRC1");
        (await locks.GetLocksAsync(TestContext.Current.CancellationToken)).Result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("thing")]
    [InlineData("Thing")]
    public async Task Ambiguous_entity_is_preserved_while_an_unambiguous_entity_in_the_same_batch_rebases(string requestedType)
    {
        var db = new Harness(true);
        db.Seed("entity-2");
        var collision = db.Entry("other-init", "entity-1", "Init", Harness.Start, new JObject { ["Value"] = 99 });
        collision.EntityType = "thing";
        db.History.Add(collision);
        var original = JArray.FromObject(db.History.Where(entry => entry.EntityId == "entity-1")).ToString();

        var results = await db.Rebase(sourceEntityType: requestedType);

        var ambiguous = results.Single(result => result.EntityId == "entity-1");
        ambiguous.Success.Should().BeFalse();
        ambiguous.FailureReason.Should().Contain("AMBIGUOUS_SOURCE_ENTITY_TYPE");
        ambiguous.ArchivedTrackingEntryCount.Should().Be(0);
        JArray.FromObject(db.History.Where(entry => entry.EntityId == "entity-1")).ToString().Should().Be(original);
        results.Single(result => result.EntityId == "entity-2").Success.Should().BeTrue();
        db.Deleted.Should().OnlyContain(entry => entry.EntityId == "entity-2");
    }

    [Fact]
    public async Task Different_stored_types_on_different_ids_are_rebased_in_their_own_partitions()
    {
        var db = new Harness(true);
        db.Seed("entity-2");
        foreach (var entry in db.History.Where(entry => entry.EntityId == "entity-2")) entry.EntityType = "thing";

        var results = await db.Rebase(sourceEntityType: "THING");

        results.Should().HaveCount(2).And.OnlyContain(result => result.Success);
        results.Single(result => result.EntityId == "entity-1").EntityType.Should().Be("Thing");
        results.Single(result => result.EntityId == "entity-2").EntityType.Should().Be("thing");
        db.History.Should().HaveCount(2);
        db.History.Select(entry => entry.EntityType).Should().BeEquivalentTo("Thing", "thing");
    }

    [Theory]
    [InlineData(false, "SRC1", "Thing")]
    [InlineData(false, "src1", "thing")]
    [InlineData(true, "SRC1", "Thing")]
    [InlineData(true, "sRc1", "THING")]
    public async Task Data_source_collisions_are_preserved_even_for_an_exact_case_request_while_other_ids_continue(
        bool differentType, string dataSource, string entityType)
    {
        var db = new Harness(true);
        db.Seed("entity-2");
        var collision = db.Entry("other-source-init", "entity-1", "Init", Harness.Start, new JObject { ["Value"] = 99 });
        collision.DataSource = "src1";
        if (differentType) collision.EntityType = "thing";
        db.History.Add(collision);
        var original = JArray.FromObject(db.History.Where(entry => entry.EntityId == "entity-1")).ToString();

        var results = await db.Rebase(sourceDataSource: dataSource, sourceEntityType: entityType);

        var ambiguous = results.Single(result => result.EntityId == "entity-1");
        ambiguous.Success.Should().BeFalse();
        ambiguous.FailureReason.Should().Contain("AMBIGUOUS_SOURCE_IDENTITY");
        ambiguous.ArchivedTrackingEntryCount.Should().Be(0);
        JArray.FromObject(db.History.Where(entry => entry.EntityId == "entity-1")).ToString().Should().Be(original);
        var unambiguous = results.Single(result => result.EntityId == "entity-2");
        unambiguous.Success.Should().BeTrue(unambiguous.FailureReason);
        unambiguous.DataSource.Should().Be("SRC1");
        unambiguous.EntityType.Should().Be("Thing");
        db.Deleted.Should().OnlyContain(entry => entry.EntityId == "entity-2");
    }

    [Fact]
    public async Task Different_pairs_on_different_ids_use_only_real_stored_pairs_for_queries_locks_and_baselines()
    {
        using var locks = new LockService();
        var db = new Harness(true, locks);
        db.Seed("entity-2");
        foreach (var entry in db.History.Where(entry => entry.EntityId == "entity-2"))
        {
            entry.DataSource = "src1";
            entry.EntityType = "thing";
        }
        var reads = new List<SourceRebaseIdentity>();
        db.BeforeRequest = async (request, ct) =>
        {
            if (request is GetSourceRebaseIdentitiesQuery) return;
            (await locks.GetLocksAsync(ct)).Result!.Select(item => item.Key).Should().BeEquivalentTo(
                "entities/SRC1/Thing/entity-1", "entities/SRC1/Thing/entity-2",
                "entities/src1/thing/entity-1", "entities/src1/thing/entity-2");
            if (request is GetAllTrackingEntriesForEntitiesRequest read)
                reads.Add(new(read.DataSource, read.EntityType));
        };

        var results = await db.Rebase(sourceDataSource: "sRc1", sourceEntityType: "THING");

        reads.Should().Equal(new SourceRebaseIdentity("SRC1", "Thing"), new SourceRebaseIdentity("src1", "thing"));
        results.Should().HaveCount(2).And.OnlyContain(result => result.Success);
        foreach (var result in results)
        {
            var baseline = db.History.Single(entry => entry.EntityId == result.EntityId);
            baseline.DataSource.Should().Be(result.DataSource);
            baseline.EntityType.Should().Be(result.EntityType);
            result.ArchivedTrackingEntries.Should().OnlyContain(entry => entry.DataSource == result.DataSource && entry.EntityType == result.EntityType);
        }
        results.Single(result => result.EntityId == "entity-1").DataSource.Should().Be("SRC1");
        results.Single(result => result.EntityId == "entity-2").DataSource.Should().Be("src1");
        (await locks.GetLocksAsync(TestContext.Current.CancellationToken)).Result.Should().BeEmpty();
    }

    [Fact]
    public async Task Dotted_source_and_type_names_are_resolved_without_splitting_or_lowercasing_the_stored_identity()
    {
        var db = new Harness(true);
        foreach (var entry in db.History)
        {
            entry.DataSource = "CRM.Europe";
            entry.EntityType = "Sales.Customer";
        }

        var result = (await db.Rebase(sourceDataSource: "crm.europe", sourceEntityType: "sales.customer")).Single();

        result.Success.Should().BeTrue(result.FailureReason);
        result.DataSource.Should().Be("CRM.Europe");
        result.EntityType.Should().Be("Sales.Customer");
        db.History.Should().ContainSingle().Which.DataSource.Should().Be("CRM.Europe");
        db.History.Single().EntityType.Should().Be("Sales.Customer");
    }

    [Fact]
    public async Task Identity_resolution_keeps_entity_ids_case_sensitive()
    {
        var db = new Harness(true);
        var original = JArray.FromObject(db.History).ToString();

        var result = (await db.Rebase(sourceDataSource: "src1", sourceEntityType: "thing", requestedEntityIds: ["ENTITY-1"])).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("NOT_FOUND");
        db.CheckpointCalls.Should().Be(0);
        JArray.FromObject(db.History).ToString().Should().Be(original);
    }

    [Fact]
    public async Task Acquisition_failure_reports_the_exact_resolved_pair_without_reading_history()
    {
        using var real = new LockService();
        var locks = RebaseBatchLockTests.Forward(real);
        locks.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromResult(new Response<List<ProcessingLock>>(false)));
        var db = new Harness(true, locks);
        db.BeforeRequest = (request, _) =>
        {
            request.Should().BeOfType<GetSourceRebaseIdentitiesQuery>();
            return Task.CompletedTask;
        };

        var result = (await db.Rebase(sourceDataSource: "src1", sourceEntityType: "thing")).Single();

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("acquire");
        result.DataSource.Should().Be("SRC1");
        result.EntityType.Should().Be("Thing");
        db.CheckpointCalls.Should().Be(0);
    }

    [Fact]
    public async Task Identity_discovery_is_parameterized_and_preserves_exact_stored_pairs()
    {
        var dataService = Substitute.For<IPartitionedDataService<ChangeTrackingEntry>>();
        dataService.WhereParameterizedAsync(cancellationToken: TestContext.Current.CancellationToken).ReturnsForAnyArgs(call =>
            {
                call.Arg<CancellationToken>().Should().Be(TestContext.Current.CancellationToken);
                call.ArgAt<string>(0).Should().Be("STRINGEQUALS(x.DataSource, @dataSource, true) AND STRINGEQUALS(x.EntityType, @entityType, true) AND x.EntityId IN (@entityId0,@entityId1)");
                call.ArgAt<string>(2).Should().Be("DISTINCT x.DataSource, x.EntityType");
                call.ArgAt<IEnumerable<QueryParameter>>(1).Select(parameter => parameter.Value).Should().Equal("SRC'1", "sales.customer", "id'1", "id2");
                return Task.FromResult(new WhereResults<ChangeTrackingEntry>
                {
                    Results = [new() { DataSource = "src'1", EntityType = "sales.customer" }, new() { DataSource = "SRC'1", EntityType = "Sales.Customer" },
                        new() { DataSource = "SRC'1", EntityType = "Sales.Customer" }]
                });
            });

        var identities = await new GetSourceRebaseIdentitiesQueryHandler(dataService).HandleAsync(new()
        {
            DataSource = "SRC'1", EntityType = "sales.customer", EntityIds = ["id'1", "id2"]
        }, TestContext.Current.CancellationToken);

        identities.Should().Equal(new SourceRebaseIdentity("SRC'1", "Sales.Customer"), new SourceRebaseIdentity("src'1", "sales.customer"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_cancelled_discovery_does_not_acquire_locks_or_change_history(bool cancel)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var real = new LockService();
        var locks = RebaseBatchLockTests.Forward(real);
        var db = new Harness(true, locks);
        var original = JArray.FromObject(db.History).ToString();
        var failure = new InvalidOperationException("identity query failed");
        db.BeforeRequest = (request, _) =>
        {
            request.Should().BeOfType<GetSourceRebaseIdentitiesQuery>();
            if (cancel)
            {
                cancellation.Cancel();
                return Task.CompletedTask; // Query returns despite cancellation.
            }
            return Task.FromException(failure);
        };

        Func<Task> run = () => db.RebaseWithCancellation(cancellation.Token, sourceEntityType: "thing");

        if (cancel) await run.Should().ThrowAsync<OperationCanceledException>();
        else (await run.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        locks.ReceivedCalls().Should().BeEmpty();
        db.CreateCalls.Should().Be(0);
        db.DeleteCalls.Should().Be(0);
        JArray.FromObject(db.History).ToString().Should().Be(original);
    }

    [Fact]
    public async Task Discovery_checks_cancellation_even_if_the_query_returns_after_cancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var dataService = Substitute.For<IPartitionedDataService<ChangeTrackingEntry>>();
        dataService.WhereParameterizedAsync(cancellationToken: cancellation.Token).ReturnsForAnyArgs(_ =>
            {
                cancellation.Cancel();
                return Task.FromResult(new WhereResults<ChangeTrackingEntry> { Results = [] });
            });

        Func<Task> run = () => new GetSourceRebaseIdentitiesQueryHandler(dataService).HandleAsync(new()
        {
            DataSource = "SRC", EntityType = "thing", EntityIds = ["id"]
        }, cancellation.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
        dataService.ReceivedCalls().Should().ContainSingle();
    }

    internal static List<SourceRebaseIdentity> IdentityPairs(IEnumerable<ChangeTrackingEntry> history, GetSourceRebaseIdentitiesQuery query)
    {
        return history.Where(entry => string.Equals(entry.DataSource, query.DataSource, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.EntityType, query.EntityType, StringComparison.OrdinalIgnoreCase) && query.EntityIds.Contains(entry.EntityId))
            .Select(entry => new SourceRebaseIdentity(entry.DataSource, entry.EntityType)).Distinct()
            .OrderBy(identity => identity.DataSource, StringComparer.Ordinal).ThenBy(identity => identity.EntityType, StringComparer.Ordinal).ToList();
    }
}
