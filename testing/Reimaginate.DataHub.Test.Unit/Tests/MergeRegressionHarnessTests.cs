using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

// Verify the completion controls themselves so regression tests cannot depend on two simultaneous writes.
[Trait("Category", "MergeRegression")]
public class MergeRegressionHarnessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completion_controls_accept_consolidated_or_sequential_writes(bool sequential)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst: true);
        db.SeedBooking("booking-1", "entity-1");
        if (sequential)
        {
            var first = await db.PersistAsync(new UpsertDataHubEntitiesCommand { Entities = [Document(db, "entity-1", cancelled: false)] });
            first.Failures.Should().BeEmpty();
        }

        var last = await db.PersistAsync(new UpsertDataHubEntitiesCommand { Entities = [Document(db, "entity-1", cancelled: true)] });

        last.Failures.Should().BeEmpty();
        db.Stored["entity-1"].Value<decimal>("Total").Should().Be(0);
        db.CompletedWrites.Should().HaveCount(sequential ? 2 : 1);
    }

    [Theory]
    [InlineData(false, "entity-1", "entity-2")]
    [InlineData(true, "entity-2", "entity-1")]
    public async Task Concurrent_writes_are_completed_in_the_selected_source_version_order(
        bool newestWriteFirst, string firstEntity, string lastEntity)
    {
        using var db = new MergeRegressionHarness(newestWriteFirst);
        db.SeedBooking("booking-1", "entity-1");
        db.SeedBooking("booking-2", "entity-2");

        var response = await db.PersistAsync(new UpsertDataHubEntitiesCommand
        {
            Entities = [Document(db, "entity-1", cancelled: false), Document(db, "entity-2", cancelled: true)]
        });

        response.Failures.Should().BeEmpty();
        db.CompletedWrites.Select(w => w.EntityId).Should().Equal(firstEntity, lastEntity);
        db.CompletedWrites.First().OtherEntityWasPending.Should().BeTrue();
    }

    private static JObject Document(MergeRegressionHarness db, string entityId, bool cancelled)
    {
        var document = (JObject)db.Stored[entityId].DeepClone();
        document.Merge((cancelled ? MergeRegressionHarness.Cancelled() : MergeRegressionHarness.Booked()).Data,
            new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        return document;
    }
}
