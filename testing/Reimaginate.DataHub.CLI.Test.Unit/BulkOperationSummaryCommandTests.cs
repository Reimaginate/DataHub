using System.CommandLine;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.CLI.Tools.Config;
using Reimaginate.DataHub.CLI.Tools.Commands.Active;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class BulkOperationSummaryCommandTests
{
    [Fact]
    public async Task Resolution_promises_delete_table_output_prints_summary_without_success_rows()
    {
        var output = await InvokeResolutionPromiseDelete(["resolution-promises", "delete", "--ids", "promise-1", "--yes"]);

        output.Should().Contain("matched 1, deleted 1, failed 0");
        output.Should().NotContain("promise-1");
        output.Should().NotContain("Deleted.");
    }

    [Fact]
    public async Task Resolution_promises_delete_json_output_still_emits_full_rows()
    {
        var output = await InvokeResolutionPromiseDelete(["resolution-promises", "delete", "--ids", "promise-1", "--yes", "--output", "json"]);

        output.Should().Contain("promise-1");
        output.Should().Contain("Deleted");
    }

    private static async Task<string> InvokeResolutionPromiseDelete(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(new SuccessfulResolutionPromiseDeleteCliApi());
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();

        var rootCommand = new RootCommand();
        rootCommand.AddDataHubTools(serviceProvider);

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
            var exitCode = await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration(), TestContext.Current.CancellationToken);
            exitCode.Should().Be(0);
        }
        finally
        {
            AnsiConsole.Console = originalAnsiConsole;
            Console.SetOut(originalOutput);
        }

        return output.ToString();
    }

    private sealed class SuccessfulResolutionPromiseDeleteCliApi : ICLIApi
    {
        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            object response = new DeleteResolutionPromisesResponse
            {
                MatchedCount = 1,
                DeletedCount = 1,
                Results =
                [
                    new DeleteResolutionPromiseResult
                    {
                        PromiseId = "promise-1",
                        DataHubEntityType = "Contact",
                        DataHubEntityId = "contact-1",
                        EntityReferencePath = "Parent",
                        DataSource = "SRC",
                        SourceEntityType = "Contact",
                        SourceEntityId = "source-1",
                        TargetEntityType = "Contact",
                        Status = "Deleted",
                        Reason = "Deleted."
                    }
                ]
            };

            return Task.FromResult((T)response);
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
