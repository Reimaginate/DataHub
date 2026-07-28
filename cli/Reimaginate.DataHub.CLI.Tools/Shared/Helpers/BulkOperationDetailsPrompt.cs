using System.Globalization;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Helpers;

public enum BulkOperationDetailsAction
{
    Save,
    View,
    Ignore
}

public static class BulkOperationDetailsPrompt
{
    public static void Show<T>(
        IEnumerable<T>? details,
        IEnumerable<string> columns,
        string filePrefix,
        string detailLabel,
        BulkOperationDetailsAction? actionOverride = null,
        Func<DateTimeOffset>? timestampProvider = null,
        string? outputDirectory = null)
    {
        var rows = details?.ToList() ?? [];
        var displayColumns = columns?.ToList() ?? [];
        if (rows.Count == 0)
        {
            return;
        }

        if (CliOutputContext.Format != CliOutputFormat.Table)
        {
            ConsoleHelper.PrintTable(rows, displayColumns);
            return;
        }

        if (actionOverride == null && Console.IsInputRedirected)
        {
            return;
        }

        var action = actionOverride ?? PromptForAction(rows.Count, detailLabel);
        switch (action)
        {
            case BulkOperationDetailsAction.Save:
                SaveDetails(rows, filePrefix, timestampProvider, outputDirectory);
                break;

            case BulkOperationDetailsAction.View:
                ConsoleHelper.PrintTable(rows, displayColumns);
                break;
        }
    }

    private static BulkOperationDetailsAction PromptForAction(int count, string detailLabel)
    {
        var save = "Save details";
        var view = "View details";
        var ignore = "Ignore";
        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"{count} {Markup.Escape(detailLabel)} available.")
                .AddChoices(save, view, ignore));

        if (selection == save)
        {
            return BulkOperationDetailsAction.Save;
        }

        return selection == view ? BulkOperationDetailsAction.View : BulkOperationDetailsAction.Ignore;
    }

    private static void SaveDetails<T>(
        List<T> rows,
        string filePrefix,
        Func<DateTimeOffset>? timestampProvider,
        string? outputDirectory)
    {
        var timestamp = (timestampProvider ?? (() => DateTimeOffset.Now))().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var safePrefix = NormalizeFilePrefix(filePrefix);
        var fileName = $"{safePrefix}-{timestamp}.json";
        var targetDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? Directory.GetCurrentDirectory() : outputDirectory;
        Directory.CreateDirectory(targetDirectory);
        var path = Path.Combine(targetDirectory, fileName);

        File.WriteAllText(path, JsonConvert.SerializeObject(rows, Formatting.Indented));
        AnsiConsole.MarkupLine($"Saved details to [yellow]{Markup.Escape(Path.GetFullPath(path))}[/]");
    }

    private static string NormalizeFilePrefix(string filePrefix)
    {
        var prefix = string.IsNullOrWhiteSpace(filePrefix) ? "bulk-operation-details" : filePrefix.Trim();
        var chars = prefix
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var safePrefix = new string(chars).Trim('-', '.', '_');
        if (string.IsNullOrWhiteSpace(safePrefix))
        {
            safePrefix = "bulk-operation-details";
        }

        return safePrefix.StartsWith("datahub-", StringComparison.OrdinalIgnoreCase) ? safePrefix : $"datahub-{safePrefix}";
    }
}
