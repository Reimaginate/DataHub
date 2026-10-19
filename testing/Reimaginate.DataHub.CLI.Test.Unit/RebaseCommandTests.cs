using System.CommandLine;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Reimaginate.DataHub.CLI.Tools.Commands.Rebase;
using Reimaginate.DataHub.CLI.Tools.Config;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class RebaseCommandTests
{
    [Theory]
    [InlineData("CRM", "Customer")]
    [InlineData("CRM.Europe", "Sales.Customer")]
    public async Task Source_rebase_preserves_the_full_type_from_normalized_keys_on_every_page(string dataSource, string storedType)
    {
        using var console = new CapturedConsole(confirm: true);
        var api = new RebaseApi("success", sourceDataSource: dataSource, sourceEntityType: storedType, paged: true);
        using var services = CreateServices(api);
        var arguments = Arguments(source: true);
        arguments[Array.IndexOf(arguments, "SRC")] = dataSource;

        var code = await CreateRoot(services).Parse(arguments).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        code.Should().Be(0);
        api.SourceRequests.Should().HaveCount(2).And.OnlyContain(request => request.DataSource == dataSource && request.EntityType == storedType.ToLowerInvariant());
        api.SourceRequests.SelectMany(request => request.EntityIds).Should().Equal("entity-a", "entity-b");
    }

    private static readonly DateTimeOffset Cutoff = DateTimeOffset.Parse("2026-10-01T00:00:00Z");

    [Theory]
    [InlineData(false, "now")]
    [InlineData(true, "now")]
    [InlineData(false, "NOW")]
    [InlineData(true, "NoW")]
    [InlineData(false, "2026-09-01T09:15:00+10:00")]
    [InlineData(true, "2026-09-01T09:15:00+10:00")]
    public async Task A_single_cutoff_is_used_across_batches_types_and_query_pages(bool source, string cutoffArgument)
    {
        using var console = new CapturedConsole(confirm: true);
        var clock = new RebaseClock();
        var api = new CutoffApi(clock, source);
        using var services = CreateServices(api, clock);
        var arguments = Arguments(source);
        arguments[Array.IndexOf(arguments, "now")] = cutoffArgument;
        var isNow = string.Equals(cutoffArgument, "now", StringComparison.OrdinalIgnoreCase);
        var expected = isNow ? RebaseClock.Start : DateTimeOffset.Parse(cutoffArgument);

        var code = await CreateRoot(services).Parse(arguments).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        code.Should().Be(0);
        api.QueryCalls.Should().Be(2);
        api.BatchSizes.Should().Equal(5000, 1, 1, 5000, 1, 1);
        api.Cutoffs.Should().HaveCount(6).And.OnlyContain(cutoff => cutoff == expected && cutoff.Offset == expected.Offset);
        api.ClockReadsBeforeQueries.Should().OnlyContain(reads => reads == (isNow ? 1 : 0));
        clock.Reads.Should().Be(isNow ? 1 : 0);
        clock.Current.Should().BeAfter(RebaseClock.Start);
    }

    [Theory]
    [InlineData("now")]
    [InlineData("2026-09-01T09:15:00+10:00")]
    public async Task Delegate_based_rebase_resolves_the_cutoff_before_loading_entities(string cutoffArgument)
    {
        using var console = new CapturedConsole(confirm: true);
        var previousFormat = CliOutputContext.Format;
        CliOutputContext.Format = CliOutputFormat.Json;
        try
        {
            var clock = new RebaseClock();
            var api = new CutoffApi(clock, source: false);
            using var services = CreateServices(api, clock);
            var isNow = cutoffArgument == "now";
            var expected = isNow ? RebaseClock.Start : DateTimeOffset.Parse(cutoffArgument);

            var code = await CliHelpers.RebaseEntitiesAsync(() =>
            {
                clock.Reads.Should().Be(isNow ? 1 : 0);
                clock.Advance();
                return Task.FromResult(CutoffApi.Entities(page: 1));
            }, cutoffArgument, services, TestContext.Current.CancellationToken);

            code.Should().Be(1); // The active command wrapper translates this legacy success value to 0.
            api.QueryCalls.Should().Be(0);
            api.BatchSizes.Should().Equal(5000, 1, 1);
            api.Cutoffs.Should().OnlyContain(cutoff => cutoff == expected && cutoff.Offset == expected.Offset);
            clock.Reads.Should().Be(isNow ? 1 : 0);
        }
        finally { CliOutputContext.Format = previousFormat; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_explicit_cutoff_fails_before_any_api_request(bool source)
    {
        using var console = new CapturedConsole(confirm: true);
        var clock = new RebaseClock();
        var api = new CutoffApi(clock, source);
        using var services = CreateServices(api, clock);
        var arguments = Arguments(source);
        arguments[Array.IndexOf(arguments, "now")] = "not-a-timestamp";

        var code = await CreateRoot(services).Parse(arguments).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        code.Should().Be(1);
        api.QueryCalls.Should().Be(0);
        api.Cutoffs.Should().BeEmpty();
        clock.Reads.Should().Be(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Cli_requests_compact_responses_and_prints_the_archived_count(bool source, bool direct)
    {
        using var console = new CapturedConsole(confirm: true);
        var api = new RebaseApi("success");
        using var services = CreateServices(api);

        var code = direct
            ? await new RebaseCommand(services).HandleCommand(source ? "SRC" : "DataHub", "Thing", ["entity-a", "entity-b"], TestContext.Current.CancellationToken)
            : await CreateRoot(services).Parse(Arguments(source)).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        code.Should().Be(0);
        (console.Output.ToString() + console.Error).Should().Contain("Archived 6 tracking entries.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_warning_is_visible_without_failing_the_command(bool direct)
    {
        using var console = new CapturedConsole(confirm: true);
        var api = new RebaseApi("warning");
        using var services = CreateServices(api);

        var code = direct
            ? await new RebaseCommand(services).HandleCommand("DataHub", "Thing", ["entity-a", "entity-b"], TestContext.Current.CancellationToken)
            : await CreateRoot(services).Parse(Arguments(source: false)).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        code.Should().Be(0);
        console.Error.ToString().Should().Contain("WARNING: DataHub/Thing/entity-b").And.Contain("STORED_ENTITY_RECOVERY");
        console.Error.ToString().Should().NotContain("invalid update");
    }

    [Theory]
    [InlineData(false, "success", 0)]
    [InlineData(true, "success", 0)]
    [InlineData(false, "partial", 1)]
    [InlineData(true, "partial", 1)]
    [InlineData(false, "failure", 1)]
    [InlineData(true, "failure", 1)]
    [InlineData(false, "later-page", 1)]
    [InlineData(true, "later-page", 1)]
    public async Task Active_rebase_reports_actual_process_exit_code(bool source, string outcome, int expectedCode)
    {
        using var console = new CapturedConsole(confirm: true);
        var api = new RebaseApi(outcome);
        using var services = CreateServices(api);
        var root = CreateRoot(services);

        var code = await root.Parse(Arguments(source)).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        code.Should().Be(expectedCode);
        api.RebaseCalls.Should().Be(outcome == "later-page" ? 2 : 1);
        if (expectedCode != 0)
        {
            console.Error.ToString().Should().Contain("entity-b").And.Contain("invalid update").And.Contain(Cutoff.ToString("O"));
        }
        console.Output.ToString().Should().Contain(outcome == "failure" ? "Rebased 0" : outcome == "success" ? "Rebased 2" : "Rebased 1");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Declined_confirmation_returns_cancelled_without_api_calls(bool source)
    {
        using var console = new CapturedConsole(confirm: false);
        var api = new RebaseApi("success");
        using var services = CreateServices(api);

        var code = await CreateRoot(services).Parse(Arguments(source)).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        code.Should().Be(130);
        api.TotalCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Active_rebase_passes_cancellation_to_api_and_returns_cancelled(bool source)
    {
        using var console = new CapturedConsole(confirm: true);
        using var cancellation = new CancellationTokenSource();
        var api = new RebaseApi("success", cancellation);
        using var services = CreateServices(api);

        var code = await CreateRoot(services).Parse(Arguments(source)).InvokeAsync(new InvocationConfiguration(), cancellation.Token);

        code.Should().Be(130);
        api.LastToken.CanBeCanceled.Should().BeTrue();
        api.LastToken.IsCancellationRequested.Should().BeTrue();
        api.RebaseCalls.Should().Be(1);
    }

    [Theory]
    [InlineData("DataHub", "success", 0)]
    [InlineData("SRC", "success", 0)]
    [InlineData("DataHub", "partial", 1)]
    [InlineData("SRC", "partial", 1)]
    [InlineData("DataHub", "failure", 1)]
    [InlineData("SRC", "failure", 1)]
    public async Task Direct_rebase_reports_success_or_failure(string source, string outcome, int expectedCode)
    {
        using var console = new CapturedConsole(confirm: true);
        var api = new RebaseApi(outcome);
        using var services = CreateServices(api);

        var code = await new RebaseCommand(services).HandleCommand(source, "Thing", ["entity-a", "entity-b"], TestContext.Current.CancellationToken);

        code.Should().Be(expectedCode);
        console.Error.ToString().Should().Contain(outcome == "failure" ? "0 succeeded, 2 failed" : outcome == "success" ? "2 succeeded, 0 failed" : "1 succeeded, 1 failed");
        if (expectedCode != 0)
        {
            console.Error.ToString().Should().Contain("invalid update").And.Contain(Cutoff.ToString("O"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_cancellation_is_not_reported_as_success(bool duringRequest)
    {
        using var console = new CapturedConsole(confirm: true);
        using var cancellation = new CancellationTokenSource();
        var api = new RebaseApi("success", duringRequest ? cancellation : null);
        using var services = CreateServices(api);
        if (!duringRequest) cancellation.Cancel();

        var code = await new RebaseCommand(services).HandleCommand("DataHub", "Thing", ["entity-a"], cancellation.Token);

        code.Should().Be(130);
        api.TotalCalls.Should().Be(duringRequest ? 1 : 0);
        console.Error.ToString().Should().Contain("0 succeeded, 0 failed, 1 unconfirmed").And.Contain("cancelled");
    }

    [Theory]
    [InlineData("DataHub")]
    [InlineData("SRC")]
    public async Task Direct_rebase_keeps_failure_status_while_processing_later_batches(string source)
    {
        using var console = new CapturedConsole(confirm: true);
        var api = new RebaseApi("partial");
        using var services = CreateServices(api);
        var ids = new[] { "entity-b" }.Concat(Enumerable.Range(1, 5000).Select(i => $"entity-{i}")).ToArray();

        var code = await new RebaseCommand(services).HandleCommand(source, "Thing", ids, TestContext.Current.CancellationToken);

        code.Should().Be(1);
        api.RebaseCalls.Should().Be(2);
        console.Error.ToString().Should().Contain("5000 succeeded, 1 failed, 0 unconfirmed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Active_rebase_returns_failure_for_api_exception(bool source)
    {
        using var console = new CapturedConsole(confirm: true);
        var api = new RebaseApi("exception");
        using var services = CreateServices(api);

        var code = await CreateRoot(services).Parse(Arguments(source)).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);

        code.Should().Be(1);
        console.Error.ToString().Should().Contain("API unavailable");
    }

    private static string[] Arguments(bool source) => source
        ? ["source-entities", "rebase", "x.entityType='Thing'", "SRC", "now", "--output", "json"]
        : ["entities", "rebase", "x.entityType='Thing'", "now", "--output", "json"];

    private static ServiceProvider CreateServices(ICLIApi api, TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        services.AddSingleton(api);
        if (clock != null) services.AddSingleton(clock);
        return services.BuildServiceProvider();
    }

    private static RootCommand CreateRoot(IServiceProvider services)
    {
        var root = new RootCommand();
        root.AddDataHubTools(services);
        return root;
    }

    private sealed class RebaseClock : TimeProvider
    {
        public static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        public DateTimeOffset Current { get; private set; } = Start;
        public int Reads { get; private set; }
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() { Reads++; return Current; }
        public void Advance() => Current = Current.AddMinutes(10);
    }

    private sealed class CutoffApi(RebaseClock clock, bool source) : ICLIApi
    {
        public int QueryCalls { get; private set; }
        public List<int> ClockReadsBeforeQueries { get; } = [];
        public List<int> BatchSizes { get; } = [];
        public List<DateTimeOffset> Cutoffs { get; } = [];

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            clock.Advance();
            if (typeof(T) == typeof(GetEntitiesResponse))
            {
                ClockReadsBeforeQueries.Add(clock.Reads);
                var query = JsonConvert.DeserializeObject<GetEntitiesWhereRequest>(message.Data)!;
                QueryCalls++;
                query.ContinuationToken.Should().Be(QueryCalls == 1 ? null : "page-2");
                return Task.FromResult((T)(object)new GetEntitiesResponse
                {
                    Success = true, Results = Entities(QueryCalls), ResultCount = 10004,
                    MoreResultsAvailable = QueryCalls == 1, ContinuationToken = QueryCalls == 1 ? "page-2" : null
                });
            }

            var payload = JObject.Parse(message.Data);
            var cutoff = source
                ? JsonConvert.DeserializeObject<RebaseSourceEntitiesRequest>(message.Data)!.RebaseTo
                : JsonConvert.DeserializeObject<RebaseDataHubEntitiesRequest>(message.Data)!.RebaseTo;
            Cutoffs.Add(cutoff!.Value);
            var ids = payload["EntityIds"]!.ToObject<List<string>>()!;
            BatchSizes.Add(ids.Count);
            payload.Value<bool>("IncludeArchivedTrackingEntries").Should().BeFalse();
            var results = ids.Select(id => new RebaseEntityTrackingResult
            {
                Success = true, DataSource = source ? "SRC" : "DataHub",
                EntityType = payload.Value<string>("EntityType"), EntityId = id
            }).ToList();
            object response = source ? new RebaseSourceEntitiesResponse { Results = results }
                : new RebaseDataHubEntitiesResponse { Results = results };
            return Task.FromResult((T)response);
        }

        public static List<JObject> Entities(int page) => Enumerable.Range(0, 5002).Select(index =>
        {
            var type = index < 5001 ? "ThingA" : "ThingB";
            var id = $"page-{page}-entity-{index}";
            return new JObject
            {
                ["id"] = id, ["entityType"] = type,
                ["alternateKeys"] = new JArray(new JObject { ["Key"] = $"src.{type.ToLowerInvariant()}", ["Value"] = id })
            };
        }).ToList();
    }

    private sealed class RebaseApi(string outcome, CancellationTokenSource? cancellation = null, string sourceDataSource = "SRC", string sourceEntityType = "Thing", bool paged = false) : ICLIApi
    {
        public List<RebaseSourceEntitiesRequest> SourceRequests { get; } = [];
        public int TotalCalls { get; private set; }
        public int RebaseCalls { get; private set; }
        public CancellationToken LastToken { get; private set; }
        private int _queryCalls;

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            TotalCalls++;
            LastToken = cancellationToken;
            if (typeof(T) == typeof(GetEntitiesResponse))
            {
                _queryCalls++;
                string[] ids = outcome == "later-page" || paged ? [_queryCalls == 1 ? "entity-a" : "entity-b"] : ["entity-a", "entity-b"];
                return Task.FromResult((T)(object)new GetEntitiesResponse
                {
                    Success = true,
                    ResultCount = 2,
                    Results = ids.Select(id => new JObject
                    {
                        ["id"] = id, ["entityType"] = "Thing",
                        ["alternateKeys"] = new JArray(new JObject { ["Key"] = $"{sourceDataSource}.{sourceEntityType}".ToLowerInvariant(), ["Value"] = id })
                    }).ToList(),
                    MoreResultsAvailable = (outcome == "later-page" || paged) && _queryCalls == 1,
                    ContinuationToken = _queryCalls == 1 ? "page-2" : null
                });
            }

            RebaseCalls++;
            JObject.Parse(message.Data).Value<bool?>("IncludeArchivedTrackingEntries").Should().Be(false);
            if (outcome == "exception") throw new InvalidOperationException("API unavailable");
            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Request did not receive the cancellation token.");
            }
            var source = typeof(T) == typeof(RebaseSourceEntitiesResponse);
            if (source) SourceRequests.Add(JsonConvert.DeserializeObject<RebaseSourceEntitiesRequest>(message.Data)!);
            var idsToRebase = JObject.Parse(message.Data)["EntityIds"]!.ToObject<string[]>()!;
            var results = idsToRebase.Select(id => new RebaseEntityTrackingResult
            {
                DataSource = source ? "SRC" : "DataHub", EntityType = "Thing", EntityId = id,
                Success = outcome is "success" or "warning" || outcome != "failure" && id != "entity-b",
                Warning = outcome == "warning" ? "STORED_ENTITY_RECOVERY: Stored entity used as authoritative baseline." : null,
                ArchivedTrackingEntryCount = 3,
                FailureReason = "invalid update", RebasedTo = Cutoff
            }).ToList();
            object response = source
                ? new RebaseSourceEntitiesResponse { Results = results }
                : new RebaseDataHubEntitiesResponse { Results = results };
            return Task.FromResult((T)response);
        }
    }

    // Supply confirmation keys without relying on a physical console or adding a package.
    private sealed class CapturedConsole : IDisposable
    {
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        private readonly IAnsiConsole _originalAnsi = AnsiConsole.Console;
        private readonly TextWriter _originalOutput = Console.Out;
        private readonly TextWriter _originalError = Console.Error;

        public CapturedConsole(bool confirm)
        {
            Console.SetOut(Output);
            Console.SetError(Error);
            var real = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(Output), Interactive = InteractionSupport.Yes
            });
            var input = Substitute.For<IAnsiConsoleInput>();
            var keys = new Queue<ConsoleKeyInfo?>([
                new ConsoleKeyInfo(confirm ? 'y' : 'n', confirm ? ConsoleKey.Y : ConsoleKey.N, false, false, false),
                new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)
            ]);
            input.ReadKey(Arg.Any<bool>()).Returns(_ => keys.Dequeue());
            input.ReadKeyAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(keys.Dequeue()));
            var console = Substitute.For<IAnsiConsole>();
            console.Profile.Returns(real.Profile);
            console.Cursor.Returns(real.Cursor);
            console.Pipeline.Returns(real.Pipeline);
            console.ExclusivityMode.Returns(real.ExclusivityMode);
            console.Input.Returns(input);
            console.When(c => c.Write(Arg.Any<IRenderable>())).Do(call => real.Write(call.Arg<IRenderable>()!));
            AnsiConsole.Console = console;
        }

        public void Dispose()
        {
            AnsiConsole.Console = _originalAnsi;
            Console.SetOut(_originalOutput);
            Console.SetError(_originalError);
            Output.Dispose();
            Error.Dispose();
        }
    }
}
