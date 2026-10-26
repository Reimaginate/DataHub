using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;
using LockService = Reimaginate.ProcessingLockService.ProcessingLockService;

namespace Reimaginate.DataHub.Test.Unit.Tests;

// Production merge handlers and the real lock service, with local database I/O.
[Trait("Category", "MergeRegression")]
public class NewEntityFollowUpMergeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task New_entity_follow_up_uses_one_destination_lock_and_persists_latest_snapshot(bool tracked, bool reversed, bool mixed)
    {
        using var realLocks = new LockService();
        var locks = RebaseBatchLockTests.Forward(realLocks);
        using var db = new MergeRegressionHarness(false, locks);
        var booked = MergeRegressionHarness.Booked();
        var cancelled = MergeRegressionHarness.Cancelled();
        if (mixed) db.SeedBooking("existing", "existing-id");
        db.SeedBooking("control", "control-id");
        var controlBefore = db.Stored["control-id"].DeepClone();
        var requests = reversed ? new[] { cancelled, booked }.ToList() : new[] { booked, cancelled }.ToList();
        if (mixed) requests.Insert(1, MergeRegressionHarness.Cancelled("existing"));

        var response = await db.MergePublicAsync(tracked, requests);

        var acquisitions = locks.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IProcessingLockService.WaitForLocksAsync))
            .Select(call => (List<string>)call.GetArguments()[0]!).ToList();
        output.WriteLine($"Lock acquisitions: {string.Join("; ", acquisitions.Select(keys => $"[{string.Join(", ", keys)}]"))}");
        output.WriteLine($"Response: Success={response.Success}; FailureReason={response.FailureReason}; Results={response.Results.Count}");

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().NotContain(result => MergeOutcomes.IsFailure(result.MergeOutcome));
        var createdId = response.Results.Single(result => result.MergeOutcome == MergeOutcomes.NewEntityCreated).DataHubEntityId;
        createdId.Should().NotBe(booked.SourceEntityId);
        var saved = db.Stored[createdId];
        saved.Value<decimal>("Total").Should().Be(0);
        saved["BookingLines"]![0]!.Value<string>("Status").Should().Be("Cancelled");
        db.Stored.Should().HaveCount(mixed ? 3 : 2);
        JToken.DeepEquals(db.Stored["control-id"], controlBefore).Should().BeTrue();
        if (mixed) db.Stored["existing-id"].Value<decimal>("Total").Should().Be(0);

        acquisitions.Should().HaveCount(2, "source locks and destination locks each have one owner");
        acquisitions[0].Should().OnlyContain(key => key.StartsWith("entities/TestSource/Reservation/"));
        var destinationKeys = new List<string> { $"entities/DataHub/Booking/{createdId}" };
        if (mixed) destinationKeys.Add("entities/DataHub/Booking/existing-id");
        acquisitions[1].Should().BeEquivalentTo(destinationKeys);
        locks.ReceivedCalls().Where(call => call.GetMethodInfo().Name == nameof(IProcessingLockService.WaitForLocksAsync))
            .Should().OnlyContain(call => Equals(call.GetArguments()[1], "regression"));
        (await realLocks.GetLocksAsync(TestContext.Current.CancellationToken)).Result.Should().BeEmpty();
    }

    [Fact]
    public async Task Single_new_booking_succeeds_with_the_real_lock_service()
    {
        using var locks = new LockService();
        using var db = new MergeRegressionHarness(false, locks);

        var response = await db.MergePublicAsync(false, [MergeRegressionHarness.Booked()]);

        response.Success.Should().BeTrue(response.FailureReason);
        response.Results.Should().ContainSingle();
        db.Stored.Values.Should().ContainSingle().Which.Value<decimal>("Total").Should().Be(42m);
        (await locks.GetLocksAsync(TestContext.Current.CancellationToken)).Result.Should().BeEmpty();
    }

    [Fact]
    public async Task Real_lock_service_rejects_empty_keys_before_acquisition()
    {
        using var locks = new LockService();

        var response = await locks.WaitForLocksAsync([], "new-booking-reproduction", TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.FailureReason!.ShortCode.Should().Be(FailureShortCodes.BadRequest);
        response.FailureReason.Details.Should().Be("Provided keys were null or empty");
        (await locks.GetLocksAsync(TestContext.Current.CancellationToken)).Result.Should().BeEmpty();
    }
}
