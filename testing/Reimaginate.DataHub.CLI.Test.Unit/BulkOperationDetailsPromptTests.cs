using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class BulkOperationDetailsPromptTests
{
    [Fact]
    public void Show_with_no_rows_does_not_save_or_print_details()
    {
        using var temp = new TempDirectory();
        var output = CaptureAnsiOutput(() =>
        {
            BulkOperationDetailsPrompt.Show(
                Array.Empty<DetailRow>(),
                [nameof(DetailRow.Id), nameof(DetailRow.Reason)],
                "test-details",
                "test detail(s)",
                BulkOperationDetailsAction.Save,
                () => new DateTimeOffset(2026, 6, 20, 14, 35, 0, TimeSpan.Zero),
                temp.Path);
        });

        output.Should().BeEmpty();
        Directory.GetFiles(temp.Path).Should().BeEmpty();
    }

    [Fact]
    public void Show_with_view_action_prints_table_details()
    {
        var output = CaptureAnsiOutput(() =>
        {
            BulkOperationDetailsPrompt.Show(
                [new DetailRow { Id = "row-1", Reason = "failed" }],
                [nameof(DetailRow.Id), nameof(DetailRow.Reason)],
                "test-details",
                "test detail(s)",
                BulkOperationDetailsAction.View);
        });

        output.Should().Contain("row-1");
        output.Should().Contain("failed");
    }

    [Fact]
    public void Show_with_save_action_writes_full_details_to_timestamped_json()
    {
        using var temp = new TempDirectory();
        CaptureAnsiOutput(() =>
        {
            BulkOperationDetailsPrompt.Show(
                [new DetailRow { Id = "row-1", Reason = "failed" }],
                [nameof(DetailRow.Id)],
                "test details",
                "test detail(s)",
                BulkOperationDetailsAction.Save,
                () => new DateTimeOffset(2026, 6, 20, 14, 35, 0, TimeSpan.Zero),
                temp.Path);
        });

        var file = Path.Combine(temp.Path, "datahub-test-details-20260620-143500.json");
        File.Exists(file).Should().BeTrue();
        var json = JArray.Parse(File.ReadAllText(file));
        json.Single()![nameof(DetailRow.Id)]!.Value<string>().Should().Be("row-1");
        json.Single()![nameof(DetailRow.Reason)]!.Value<string>().Should().Be("failed");
    }

    [Fact]
    public void Show_with_structured_output_prints_rows_and_does_not_prompt_or_save()
    {
        using var temp = new TempDirectory();
        var previousFormat = CliOutputContext.Format;
        var output = new StringWriter();
        var originalOutput = Console.Out;
        try
        {
            CliOutputContext.Format = CliOutputFormat.Json;
            Console.SetOut(output);

            BulkOperationDetailsPrompt.Show(
                [new DetailRow { Id = "row-1", Reason = "failed" }],
                [nameof(DetailRow.Id)],
                "test-details",
                "test detail(s)",
                BulkOperationDetailsAction.Save,
                () => new DateTimeOffset(2026, 6, 20, 14, 35, 0, TimeSpan.Zero),
                temp.Path);
        }
        finally
        {
            Console.SetOut(originalOutput);
            CliOutputContext.Format = previousFormat;
        }

        output.ToString().Should().Contain("row-1");
        Directory.GetFiles(temp.Path).Should().BeEmpty();
    }

    private static string CaptureAnsiOutput(Action action)
    {
        var previousFormat = CliOutputContext.Format;
        var originalAnsiConsole = AnsiConsole.Console;
        var output = new StringWriter();
        try
        {
            CliOutputContext.Format = CliOutputFormat.Table;
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new FixedWidthAnsiConsoleOutput(output)
            });
            action();
            return output.ToString();
        }
        finally
        {
            AnsiConsole.Console = originalAnsiConsole;
            CliOutputContext.Format = previousFormat;
        }
    }

    private sealed class DetailRow
    {
        public string Id { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"datahub-cli-test-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class FixedWidthAnsiConsoleOutput(TextWriter writer) : IAnsiConsoleOutput
    {
        public TextWriter Writer { get; } = writer;
        public bool IsTerminal => false;
        public int Width => 200;
        public int Height => 60;

        public void SetEncoding(System.Text.Encoding encoding)
        {
        }
    }
}
