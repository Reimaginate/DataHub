using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Services.RebaseTracking;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Cosmos;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class CosmosRebaseCheckpointStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_only_checkpoint_commits_or_confirms_a_lost_response_without_deletes_or_write_retries(bool lostResponse)
    {
        var db = new Harness { ThrowAfterCommit = lostResponse, ExistingBaselineMatches = true };
        db.Command.EntriesToDelete.Clear();

        var result = await db.Store.CommitAsync(db.Command, Token);

        result.Success.Should().BeTrue(result.FailureReason);
        db.Deletes.Should().BeEmpty();
        db.Written.Should().NotBeNull();
        await db.Batch.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
        if (lostResponse)
            await db.Container.Received(1).ReadItemStreamAsync("checkpoint", Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmation_is_bounded_even_when_the_backend_ignores_cancellation(bool stalledBody)
    {
        var db = new Harness { ThrowAfterCommit = true };
        var pending = new TaskCompletionSource<ResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var body = new StalledContent();
        db.Container.ReadItemStreamAsync(Arg.Any<string>(), Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>())
            .Returns(stalledBody ? Task.FromResult(new ResponseMessage(HttpStatusCode.OK) { Content = body }) : pending.Task);

        var result = await db.Store.CommitAsync(db.Command, Token).WaitAsync(TimeSpan.FromSeconds(15), Token);

        result.Success.Should().BeFalse();
        result.OutcomeUnknown.Should().BeTrue();
        body.Complete();
        pending.SetResult(new ResponseMessage(HttpStatusCode.NotFound));
        await db.Batch.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }

    private sealed class StalledContent : MemoryStream
    {
        private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => finished.Task;
        public void Complete() => finished.TrySetResult();
    }

    [Fact]
    public async Task Uses_one_transaction_with_the_full_hierarchical_key_and_conditional_deletes()
    {
        var db = new Harness();
        db.Command.Baseline.EntityId = "entity/with/slash";
        db.Command.EntriesToDelete.ForEach(entry => entry.EntityId = "entity/with/slash");

        var result = await db.Store.CommitAsync(db.Command, Token);

        result.Success.Should().BeTrue(result.FailureReason);
        JArray.Parse(db.Partition!.Value.ToString()).Values<string>().Should().Equal("SRC", "Thing", "entity/with/slash");
        db.Deletes.Should().Equal(("old-init", "etag-1"), ("old-update", "etag-2"));
        db.Written!["Data"]!["DateLike"]!.Type.Should().Be(JTokenType.String);
        db.Written["Data"]!["Optional"]!.Type.Should().Be(JTokenType.Null);
        await db.Batch.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
        db.Container.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name == "DeleteItemStreamAsync" || call.GetMethodInfo().Name == "CreateItemStreamAsync");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.PreconditionFailed)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_failed_transaction_is_not_followed_by_individual_writes_or_deletes(HttpStatusCode failure)
    {
        var db = new Harness { Status = failure };

        var result = await db.Store.CommitAsync(db.Command, Token);

        result.Success.Should().BeFalse();
        result.OutcomeUnknown.Should().BeFalse();
        await db.Batch.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
        db.Container.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name == "ReadItemStreamAsync");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_response_is_confirmed_using_the_checkpoint_baseline_without_retrying_the_write(bool conflict)
    {
        var db = new Harness { Status = conflict ? HttpStatusCode.Conflict : HttpStatusCode.Created, ThrowAfterCommit = !conflict, ExistingBaselineMatches = true };

        var result = await db.Store.CommitAsync(db.Command, Token);

        result.Success.Should().BeTrue(result.FailureReason);
        await db.Batch.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
        await db.Container.Received(1).ReadItemStreamAsync("checkpoint", Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_different_baseline_leaves_the_outcome_unknown_and_never_retries(bool mismatch)
    {
        var db = new Harness { ThrowAfterCommit = true, ExistingBaselineMatches = mismatch, CorruptReadback = mismatch };

        var result = await db.Store.CommitAsync(db.Command, Token);

        result.Success.Should().BeFalse();
        result.OutcomeUnknown.Should().BeTrue();
        await db.Batch.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_only_checkpoint_can_confirm_a_lost_response_by_checking_every_deleted_id()
    {
        var db = new Harness { ThrowAfterCommit = true };
        db.Command.Baseline = null!;

        var result = await db.Store.CommitAsync(db.Command, Token);

        result.Success.Should().BeTrue();
        await db.Container.Received(2).ReadItemStreamAsync(Arg.Any<string>(), Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Metadata_is_cached_and_actual_container_partition_paths_are_used()
    {
        var db = new Harness();
        db.Paths = ["/pk"];
        db.Options.SetItemPartitionKeyFunc = entry => entry.pk = "actual-partition";
        db.Command.EntriesToDelete.ForEach(entry => entry.pk = "actual-partition");

        (await db.Store.CommitAsync(db.Command, Token)).Success.Should().BeTrue();
        (await db.Store.CommitAsync(db.Command, Token)).Success.Should().BeTrue();

        JArray.Parse(db.Partition!.Value.ToString()).Values<string>().Should().Equal("actual-partition");
        await db.Container.Received(1).ReadContainerAsync(Arg.Any<ContainerRequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Histories_with_different_full_partition_keys_are_rejected_before_writing()
    {
        var db = new Harness { Paths = ["/id"] };

        var result = await db.Store.CommitAsync(db.Command, Token);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("spans logical partitions");
        db.Batch.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Oversized_checkpoints_are_rejected_without_writing(bool payload)
    {
        var db = new Harness();
        if (payload) db.Command.Baseline.Data["Large"] = new string('x', 2 * 1024 * 1024);
        else db.Command.EntriesToDelete = Enumerable.Range(0, 100).Select(index => db.Entry($"entry-{index}")).ToList();

        var result = await db.Store.CommitAsync(db.Command, Token);

        result.TooLarge.Should().BeTrue();
        db.Batch.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Server_payload_limit_failure_is_safe_to_retry_with_a_smaller_checkpoint()
    {
        var db = new Harness { Status = HttpStatusCode.RequestEntityTooLarge };

        var result = await db.Store.CommitAsync(db.Command, Token);

        result.TooLarge.Should().BeTrue();
        result.OutcomeUnknown.Should().BeFalse();
    }

    [Fact]
    public async Task Lease_loss_prevents_execution_and_does_not_trigger_commit_verification()
    {
        var db = new Harness();
        db.Command.EnsureLease = () => throw new OperationCanceledException("lease lost");

        Func<Task> run = () => db.Store.CommitAsync(db.Command, Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
        db.Batch.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name == "ExecuteAsync");
        db.Container.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name == "ReadItemStreamAsync");
    }

    [Fact]
    public async Task Cancellation_after_commit_still_allows_bounded_read_only_confirmation()
    {
        var db = new Harness { ThrowAfterCommit = true, ExistingBaselineMatches = true };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        db.OnExecute = cancellation.Cancel;

        var result = await db.Store.CommitAsync(db.Command, cancellation.Token);

        result.Success.Should().BeTrue();
        db.ReadToken.IsCancellationRequested.Should().BeFalse();
    }

    private sealed class Harness
    {
        public Container Container { get; } = Substitute.For<Container>();
        public TransactionalBatch Batch { get; } = Substitute.For<TransactionalBatch>();
        public CosmosDataServiceOptions<ChangeTrackingEntry> Options { get; }
        public CosmosRebaseCheckpointStore Store { get; }
        public CommitRebaseCheckpointCommand Command { get; }
        public List<(string Id, string Etag)> Deletes { get; } = [];
        public PartitionKey? Partition { get; private set; }
        public JObject? Written { get; private set; }
        public string[] Paths { get; set; } = ["/DataSource", "/EntityType", "/EntityId"];
        public HttpStatusCode Status { get; init; } = HttpStatusCode.Created;
        public bool ThrowAfterCommit { get; init; }
        public bool ExistingBaselineMatches { get; init; }
        public bool CorruptReadback { get; init; }
        public Action? OnExecute { get; set; }
        public CancellationToken ReadToken { get; private set; }

        public Harness()
        {
            var client = Substitute.For<CosmosClient>();
            client.ClientOptions.Returns(new CosmosClientOptions { Serializer = new DataHubDataSerializer() });
            client.GetContainer("test", "tracking").Returns(Container);
            Options = new() { CosmosClient = client, DatabaseName = "test", ContainerName = "tracking" };
            Store = new CosmosRebaseCheckpointStore(Options);
            Command = new() { Baseline = Entry("checkpoint"), EntriesToDelete = [Entry("old-init"), Entry("old-update")] };
            Command.EntriesToDelete[0]._etag = "etag-1";
            Command.EntriesToDelete[1]._etag = "etag-2";
            Container.ReadContainerAsync(Arg.Any<ContainerRequestOptions>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                var response = Substitute.For<ContainerResponse>();
                response.Resource.Returns(new ContainerProperties { Id = "tracking", PartitionKeyPaths = Paths });
                return Task.FromResult(response);
            });
            Container.CreateTransactionalBatch(Arg.Any<PartitionKey>()).Returns(call => { Partition = call.Arg<PartitionKey>(); return Batch; });
            Batch.CreateItemStream(Arg.Any<Stream>(), Arg.Any<TransactionalBatchItemRequestOptions>()).Returns(call =>
            {
                using var reader = new StreamReader(call.ArgAt<Stream>(0), leaveOpen: true);
                using var json = new Newtonsoft.Json.JsonTextReader(reader) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
                Written = JObject.Load(json);
                return Batch;
            });
            Batch.DeleteItem(Arg.Any<string>(), Arg.Any<TransactionalBatchItemRequestOptions>()).Returns(call =>
            {
                Deletes.Add((call.ArgAt<string>(0), call.ArgAt<TransactionalBatchItemRequestOptions>(1).IfMatchEtag));
                return Batch;
            });
            Batch.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                OnExecute?.Invoke();
                if (ThrowAfterCommit) return Task.FromException<TransactionalBatchResponse>(new TimeoutException("response lost"));
                var response = Substitute.For<TransactionalBatchResponse>();
                response.StatusCode.Returns(Status);
                response.IsSuccessStatusCode.Returns((int)Status < 300);
                return Task.FromResult(response);
            });
            Container.ReadItemStreamAsync(Arg.Any<string>(), Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                ReadToken = call.ArgAt<CancellationToken>(3);
                if (!ExistingBaselineMatches) return Task.FromResult(new ResponseMessage(HttpStatusCode.NotFound));
                var copy = (JObject)Written!.DeepClone();
                if (CorruptReadback) copy["Data"]!["Value"] = 999;
                return Task.FromResult(new ResponseMessage(HttpStatusCode.OK) { Content = new MemoryStream(Encoding.UTF8.GetBytes(copy.ToString())) });
            });
        }

        public ChangeTrackingEntry Entry(string id) => new()
        {
            id = id, DataSource = "SRC", EntityType = "Thing", EntityId = "entity", EntryType = "Init",
            Timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            Data = new JObject { ["Value"] = 1, ["DateLike"] = "2026-01-01T00:00:00Z", ["Optional"] = JValue.CreateNull() }
        };
    }
}
