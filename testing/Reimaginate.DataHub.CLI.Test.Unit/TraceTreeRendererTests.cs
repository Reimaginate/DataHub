using System.CommandLine;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Commands.Diagnostics;
using Reimaginate.DataHub.CLI.Tools.Config;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class TraceTreeRendererTests
{
    [Fact]
    public void Build_renders_nested_spans_orphans_and_error_details()
    {
        var output = RenderTree(SampleResponse());

        output.Should().Contain("Trace");
        output.Should().Contain("#1");
        output.Should().Contain("#2");
        output.Should().Contain("GET /api/CLI");
        output.Should().Contain("Mediator Send");
        output.Should().Contain("ERROR");
        output.Should().Contain("error=err-1");
        output.Should().Contain("category=ServerError");
        output.Should().Contain("Unparented telemetry");
        output.IndexOf("GET /api/CLI", StringComparison.Ordinal).Should().BeLessThan(output.IndexOf("Mediator Send", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteDetails_prints_message_ids_status_and_dimensions()
    {
        var output = CaptureConsole(() => TraceTreeRenderer.WriteDetails(SampleResponse(), recordNumber: 2));

        output.Should().Contain("Trace details");
        output.Should().Contain("Record #2");
        output.Should().Contain("OperationId");
        output.Should().Contain("op-1");
        output.Should().Contain("SpanId");
        output.Should().Contain("child");
        output.Should().Contain("Message");
        output.Should().Contain("Dependency failed.");
        output.Should().Contain("datahub.dependency.target");
        output.Should().Contain("cosmos");
        output.Should().Contain("err-1");
        output.Should().NotContain("Root request received.");
    }

    [Fact]
    public void WriteDetails_filters_by_span_id_and_operation_id()
    {
        var spanOutput = CaptureConsole(() => TraceTreeRenderer.WriteDetails(SampleResponse(), spanId: "child"));
        spanOutput.Should().Contain("Record #2");
        spanOutput.Should().Contain("Dependency failed.");
        spanOutput.Should().NotContain("Root request received.");

        var operationOutput = CaptureConsole(() => TraceTreeRenderer.WriteDetails(SampleResponse(), operationId: "op-2"));
        operationOutput.Should().Contain("Record #3");
        operationOutput.Should().Contain("Unhandled exception.");
        operationOutput.Should().NotContain("Dependency failed.");
    }

    [Fact]
    public async Task Diagnostics_trace_default_output_writes_spectre_tree()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(new TraceCliApi(SampleResponse()));
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var output = await CaptureConsoleAsync(async () =>
        {
            var exitCode = await rootCommand.Parse(["diagnostics", "trace", "corr-1"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);
            exitCode.Should().Be(0);
        });

        output.Should().Contain("Trace");
        output.Should().Contain("#1");
        output.Should().Contain("GET /api/CLI");
        output.Should().Contain("Mediator Send");
        output.Should().Contain("Unparented telemetry");
    }

    [Fact]
    public async Task Diagnostics_trace_details_output_filters_records_and_requests_payload_details()
    {
        var api = new TraceCliApi(SampleResponse());
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

        var output = await CaptureConsoleAsync(async () =>
        {
            var exitCode = await rootCommand.Parse(["diagnostics", "trace", "corr-1", "--details", "--record", "2", "--include-payloads"]).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);
            exitCode.Should().Be(0);
        });

        output.Should().Contain("Record #2");
        output.Should().Contain("Dependency failed.");
        output.Should().NotContain("Root request received.");
        api.LastRequest.Should().NotBeNull();
        api.LastRequest!.IncludePayloadDetails.Should().BeTrue();
    }

    private static string RenderTree(GetTraceResponse response)
        => CaptureConsole(() => AnsiConsole.Write(TraceTreeRenderer.Build(response)));

    private static string CaptureConsole(Action action)
    {
        var output = new StringWriter();
        var originalAnsiConsole = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new FixedWidthAnsiConsoleOutput(output)
        });
        try
        {
            action();
        }
        finally
        {
            AnsiConsole.Console = originalAnsiConsole;
        }

        return output.ToString();
    }

    private static async Task<string> CaptureConsoleAsync(Func<Task> action)
    {
        var output = new StringWriter();
        var originalOutput = Console.Out;
        var originalAnsiConsole = AnsiConsole.Console;
        Console.SetOut(output);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new FixedWidthAnsiConsoleOutput(output)
        });
        try
        {
            await action();
        }
        finally
        {
            AnsiConsole.Console = originalAnsiConsole;
            Console.SetOut(originalOutput);
        }

        return output.ToString();
    }

    private static GetTraceResponse SampleResponse()
        => new()
        {
            Success = true,
            CorrelationId = "corr-1",
            FromUtc = DateTimeOffset.Parse("2026-06-20T00:00:00Z"),
            ToUtc = DateTimeOffset.Parse("2026-06-21T00:00:00Z"),
            Records =
            [
                new TraceRecord
                {
                    OperationId = "op-1",
                    SpanId = "root",
                    Kind = "request",
                    Name = "GET /api/CLI",
                    StartTimeUtc = DateTimeOffset.Parse("2026-06-20T00:00:01Z"),
                    DurationMs = 120,
                    Success = true,
                    RequestType = "GetEntitiesRequest",
                    HttpStatusCode = 200,
                    CloudRoleName = "DataHub.Api",
                    Message = "Root request received.",
                    Details = new Dictionary<string, string>
                    {
                        ["datahub.endpoint"] = "cli",
                        ["custom.root"] = "root-detail"
                    }
                },
                new TraceRecord
                {
                    OperationId = "op-1",
                    SpanId = "child",
                    ParentSpanId = "root",
                    Kind = "dependency",
                    Name = "Mediator Send",
                    StartTimeUtc = DateTimeOffset.Parse("2026-06-20T00:00:02Z"),
                    DurationMs = 40,
                    Success = false,
                    ErrorId = "err-1",
                    ErrorCategory = "ServerError",
                    HttpStatusCode = 500,
                    Message = "Dependency failed.",
                    Details = new Dictionary<string, string>
                    {
                        ["datahub.dependency.target"] = "cosmos",
                        ["custom.child"] = "child-detail"
                    }
                },
                new TraceRecord
                {
                    OperationId = "op-2",
                    SpanId = "orphan",
                    ParentSpanId = "missing",
                    Kind = "exception",
                    Name = "Unhandled exception",
                    StartTimeUtc = DateTimeOffset.Parse("2026-06-20T00:00:03Z"),
                    Message = "Unhandled exception.",
                    Details = new Dictionary<string, string>
                    {
                        ["exception.type"] = "InvalidOperationException"
                    }
                }
            ]
        };

    private sealed class TraceCliApi(GetTraceResponse response) : ICLIApi
    {
        public GetTraceRequest? LastRequest { get; private set; }

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            LastRequest = JsonConvert.DeserializeObject<GetTraceRequest>(message.Data);
            return Task.FromResult((T)(object)response);
        }
    }

    private sealed class FixedWidthAnsiConsoleOutput(TextWriter writer) : IAnsiConsoleOutput
    {
        public TextWriter Writer { get; } = writer;
        public bool IsTerminal => false;
        public int Width => 120;
        public int Height => 40;
        public void SetEncoding(System.Text.Encoding encoding) { }
    }
}
