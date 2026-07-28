using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneOf;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Logs;
using Reimaginate.DataHub.CLI.Tools.Commands.Patch.Entities;
using Reimaginate.DataHub.CLI.Tools.Commands.Submit.Job;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class CliConsistencyFixTests
{
    [Fact]
    public async Task Submit_job_returns_failure_when_api_response_fails()
    {
        var api = new FixedCliApi(new SubmitJobResponse { Success = false, FailureReason = "Invalid job" });
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<ICLIApi>(api)
            .BuildServiceProvider();
        var command = new SubmitJobCommand(serviceProvider);
        var jobPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(jobPath, """{"Name":"bad-job"}""", TestContext.Current.CancellationToken);

        try
        {
            var exitCode = await command.HandleCommand(jobPath, TestContext.Current.CancellationToken);

            exitCode.Should().Be(0);
        }
        finally
        {
            File.Delete(jobPath);
        }
    }

    [Fact]
    public async Task Logs_query_save_to_writes_all_pages()
    {
        var api = new SequenceCliApi([
            new GetLogEntriesResponse
            {
                Results = [new LogEntry { id = "log-1", Type = "Sync", Timestamp = DateTimeOffset.UtcNow }],
                MoreResultsAvailable = true,
                ContinuationToken = "page-2"
            },
            new GetLogEntriesResponse
            {
                Results = [new LogEntry { id = "log-2", Type = "Patch", Timestamp = DateTimeOffset.UtcNow }]
            }
        ]);
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<ICLIApi>(api)
            .BuildServiceProvider();
        var command = new GetLogsWhereCommand(serviceProvider);
        var savePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            var exitCode = await command.HandleCommand("x.Type = 'Sync'", saveTo: savePath, dontOpen: true, cancellationToken: TestContext.Current.CancellationToken);

            exitCode.Should().Be(1);
            var saved = JArray.Parse(await File.ReadAllTextAsync(savePath, TestContext.Current.CancellationToken));
            saved.Select(token => token.Value<string>(nameof(LogEntry.id))).Should().Equal("log-1", "log-2");
            api.Messages.Select(message => JsonConvert.DeserializeObject<GetLogEntriesWhereRequest>(message.Data)!.ContinuationToken)
                .Should()
                .Equal(null, "page-2");
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [Fact]
    public async Task Logs_query_info_prints_count_without_ignoring_option()
    {
        var api = new SequenceCliApi([
            new GetLogEntriesResponse
            {
                ResultCount = 42,
                Results = []
            }
        ]);
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<ICLIApi>(api)
            .BuildServiceProvider();
        var command = new GetLogsWhereCommand(serviceProvider);

        var exitCode = await command.HandleCommand("x.Type = 'Sync'", info: true, cancellationToken: TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        var request = JsonConvert.DeserializeObject<GetLogEntriesWhereRequest>(api.Messages.Single().Data)!;
        request.GetTotalResultCount.Should().BeTrue();
    }

    [Fact]
    public async Task Patch_entities_datahub_dry_run_accepts_json_array_patch_file_without_wrapping_it()
    {
        var mediator = new RecordingMediator(request =>
        {
            var sendRequest = (SendRequest)request;
            sendRequest.Request.Should().BeOfType<GetEntitiesWhereRequest>();
            return new SendResponse
            {
                Result = JObject.FromObject(new GetEntitiesResponse
                {
                    Success = true,
                    ResultCount = 1,
                    Results =
                    [
                        new JObject
                        {
                            [nameof(DataHubEntity.id)] = "entity-1",
                            [nameof(DataHubEntity.entityType)] = "Contact"
                        }
                    ]
                })
            };
        });
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMediator>(mediator)
            .BuildServiceProvider();
        var command = new PatchEntitiesCommand(serviceProvider);
        var patchPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(patchPath, """[{"Operation":"set","Path":"Name","Value":"Updated"}]""", TestContext.Current.CancellationToken);

        try
        {
            var exitCode = await command.HandleCommand(
                "DATAHUB",
                patchPath,
                "x.entityType = 'Contact'",
                conn: null,
                container: null,
                pattern: null,
                silent: true,
                notifyagents: false,
                @continue: false,
                dryrun: true,
                yes: false,
                cancellationToken: TestContext.Current.CancellationToken);

            exitCode.Should().Be(1);
            mediator.Requests.Should().ContainSingle();
        }
        finally
        {
            File.Delete(patchPath);
        }
    }

    private sealed class FixedCliApi(object response) : ICLIApi
    {
        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
            => Task.FromResult((T)response);
    }

    private sealed class SequenceCliApi(IReadOnlyList<object> responses) : ICLIApi
    {
        private int _index;

        public List<SerializedRequest> Messages { get; } = [];

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            var response = responses[Math.Min(_index, responses.Count - 1)];
            _index++;
            return Task.FromResult((T)response);
        }
    }

    private sealed class RecordingMediator(Func<IRequest, object> responseFactory) : IMediator
    {
        public List<IRequest> Requests { get; } = [];

        public Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<object> SendAndHandleExceptions<TRequest>(TRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
            where TRequest : IRequest
            => throw new NotSupportedException();

        public Task<TResponse> SendAndHandleExceptions<TResponse>(IRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
            => throw new NotSupportedException();

        public Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            Requests.Add(request);
            return Task.FromResult(((TResponse?)responseFactory(request), (Exception?)null));
        }
    }
}
