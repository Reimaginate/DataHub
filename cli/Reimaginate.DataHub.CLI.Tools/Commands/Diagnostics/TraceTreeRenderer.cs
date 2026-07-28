using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Diagnostics;

public static class TraceTreeRenderer
{
    public static Tree Build(GetTraceResponse response)
    {
        var records = response.Records ?? [];
        var rootLabel = $"[bold]Trace[/] [grey]{Markup.Escape(response.CorrelationId ?? string.Empty)}[/] [grey]{response.FromUtc:u} - {response.ToUtc:u}[/]";
        var tree = new Tree(rootLabel);
        if (records.Count == 0)
        {
            return tree;
        }

        var recordNumbers = GetRenderOrder(response)
            .Select((record, index) => new { Record = record, Number = index + 1 })
            .ToDictionary(item => item.Record, item => item.Number);

        var nodesBySpanId = records
            .Where(record => !string.IsNullOrWhiteSpace(record.SpanId))
            .GroupBy(record => record.SpanId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.StartTimeUtc).First(), StringComparer.OrdinalIgnoreCase);

        var childrenByParent = records
            .Where(record => !string.IsNullOrWhiteSpace(record.ParentSpanId) && nodesBySpanId.ContainsKey(record.ParentSpanId))
            .GroupBy(record => record.ParentSpanId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(record => record.StartTimeUtc).ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var roots = records
            .Where(record => string.IsNullOrWhiteSpace(record.ParentSpanId))
            .Where(record => !string.IsNullOrWhiteSpace(record.SpanId))
            .OrderBy(record => record.StartTimeUtc)
            .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rootRecord in roots)
        {
            AddRecord(tree, rootRecord, records, childrenByParent, visited, recordNumbers);
        }

        var unvisited = records
            .Where(record => string.IsNullOrWhiteSpace(record.SpanId) || !visited.Contains(record.SpanId))
            .OrderBy(record => record.StartTimeUtc)
            .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unvisited.Count > 0)
        {
            var orphanNode = tree.AddNode("[yellow]Unparented telemetry[/]");
            foreach (var record in unvisited)
            {
                AddRecord(orphanNode, record, records, childrenByParent, visited, recordNumbers);
            }
        }

        return tree;
    }

    public static IReadOnlyList<TraceRecord> GetRenderOrder(GetTraceResponse response)
    {
        var records = response.Records ?? [];
        if (records.Count == 0)
        {
            return [];
        }

        var nodesBySpanId = records
            .Where(record => !string.IsNullOrWhiteSpace(record.SpanId))
            .GroupBy(record => record.SpanId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.StartTimeUtc).First(), StringComparer.OrdinalIgnoreCase);

        var childrenByParent = records
            .Where(record => !string.IsNullOrWhiteSpace(record.ParentSpanId) && nodesBySpanId.ContainsKey(record.ParentSpanId))
            .GroupBy(record => record.ParentSpanId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(record => record.StartTimeUtc).ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var roots = records
            .Where(record => string.IsNullOrWhiteSpace(record.ParentSpanId))
            .Where(record => !string.IsNullOrWhiteSpace(record.SpanId))
            .OrderBy(record => record.StartTimeUtc)
            .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ordered = new List<TraceRecord>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rootRecord in roots)
        {
            CollectRecord(rootRecord, childrenByParent, visited, ordered);
        }

        var unvisited = records
            .Where(record => string.IsNullOrWhiteSpace(record.SpanId) || !visited.Contains(record.SpanId))
            .OrderBy(record => record.StartTimeUtc)
            .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var record in unvisited)
        {
            CollectRecord(record, childrenByParent, visited, ordered);
        }

        return ordered;
    }

    public static int WriteDetails(
        GetTraceResponse response,
        int? recordNumber = null,
        string? spanId = null,
        string? operationId = null)
    {
        var matches = GetRenderOrder(response)
            .Select((record, index) => new NumberedTraceRecord(index + 1, record))
            .Where(item => !recordNumber.HasValue || item.Number == recordNumber.Value)
            .Where(item => string.IsNullOrWhiteSpace(spanId) || string.Equals(item.Record.SpanId, spanId, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(operationId) || string.Equals(item.Record.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No trace records matched detail filters.[/]");
            return 0;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Trace details[/]");
        foreach (var item in matches)
        {
            WriteRecordDetails(item.Number, item.Record);
        }

        return matches.Count;
    }

    private static void AddRecord(
        IHasTreeNodes parent,
        TraceRecord record,
        IReadOnlyCollection<TraceRecord> records,
        IReadOnlyDictionary<string, List<TraceRecord>> childrenByParent,
        HashSet<string> visited,
        IReadOnlyDictionary<TraceRecord, int> recordNumbers)
    {
        if (!string.IsNullOrWhiteSpace(record.SpanId) && !visited.Add(record.SpanId))
        {
            return;
        }

        var earliest = records.Where(candidate => candidate.StartTimeUtc != DateTimeOffset.MinValue)
            .Select(candidate => candidate.StartTimeUtc)
            .DefaultIfEmpty(record.StartTimeUtc)
            .Min();
        var node = parent.AddNode(RenderLabel(record, earliest, recordNumbers.GetValueOrDefault(record)));
        if (string.IsNullOrWhiteSpace(record.SpanId) || !childrenByParent.TryGetValue(record.SpanId, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            AddRecord(node, child, records, childrenByParent, visited, recordNumbers);
        }
    }

    private static void CollectRecord(
        TraceRecord record,
        IReadOnlyDictionary<string, List<TraceRecord>> childrenByParent,
        HashSet<string> visited,
        List<TraceRecord> ordered)
    {
        if (!string.IsNullOrWhiteSpace(record.SpanId) && !visited.Add(record.SpanId))
        {
            return;
        }

        ordered.Add(record);
        if (string.IsNullOrWhiteSpace(record.SpanId) || !childrenByParent.TryGetValue(record.SpanId, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            CollectRecord(child, childrenByParent, visited, ordered);
        }
    }

    private static string RenderLabel(TraceRecord record, DateTimeOffset earliest, int recordNumber)
    {
        var offset = record.StartTimeUtc == DateTimeOffset.MinValue ? string.Empty : $"+{Math.Max(0, (record.StartTimeUtc - earliest).TotalMilliseconds):0}ms ";
        var duration = record.DurationMs.HasValue ? $"{record.DurationMs.Value:0.#}ms " : string.Empty;
        var status = RenderStatus(record);
        var kind = string.IsNullOrWhiteSpace(record.Kind) ? "telemetry" : record.Kind;
        var name = Markup.Escape(string.IsNullOrWhiteSpace(record.Name) ? "(unnamed)" : record.Name);
        var details = BuildDetails(record);
        var number = recordNumber > 0 ? $"[grey]#{recordNumber}[/] " : string.Empty;

        return $"[grey]{offset}{duration}[/]{number}{status} [deepskyblue1]{Markup.Escape(kind)}[/] {name}{details}";
    }

    private static string RenderStatus(TraceRecord record)
    {
        if (record.Success == false || record.HttpStatusCode >= 500 || !string.IsNullOrWhiteSpace(record.ErrorId))
        {
            return "[red]ERROR[/]";
        }

        if (record.HttpStatusCode >= 400 || string.Equals(record.Severity, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            return "[yellow]WARN[/]";
        }

        return record.Success == true ? "[green]OK[/]" : "[grey]UNK[/]";
    }

    private static string BuildDetails(TraceRecord record)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(record.RequestType))
        {
            parts.Add($"request={record.RequestType}");
        }

        if (!string.IsNullOrWhiteSpace(record.ErrorId))
        {
            parts.Add($"error={record.ErrorId}");
        }

        if (!string.IsNullOrWhiteSpace(record.ErrorCategory))
        {
            parts.Add($"category={record.ErrorCategory}");
        }

        if (record.HttpStatusCode.HasValue)
        {
            parts.Add($"http={record.HttpStatusCode}");
        }

        if (!string.IsNullOrWhiteSpace(record.CloudRoleName))
        {
            parts.Add($"role={record.CloudRoleName}");
        }

        return parts.Count == 0
            ? string.Empty
            : $" [grey]({Markup.Escape(string.Join(", ", parts))})[/]";
    }

    private static void WriteRecordDetails(int recordNumber, TraceRecord record)
    {
        var table = new Table()
            .NoBorder()
            .AddColumn(new TableColumn("[grey]Field[/]"))
            .AddColumn(new TableColumn("[grey]Value[/]"));

        AddDetailRow(table, "record", $"#{recordNumber}");
        AddDetailRow(table, nameof(TraceRecord.OperationId), record.OperationId);
        AddDetailRow(table, nameof(TraceRecord.SpanId), record.SpanId);
        AddDetailRow(table, nameof(TraceRecord.ParentSpanId), record.ParentSpanId);
        AddDetailRow(table, nameof(TraceRecord.Kind), record.Kind);
        AddDetailRow(table, nameof(TraceRecord.Name), record.Name);
        AddDetailRow(table, nameof(TraceRecord.Message), record.Message);
        AddDetailRow(table, nameof(TraceRecord.StartTimeUtc), record.StartTimeUtc == DateTimeOffset.MinValue ? string.Empty : record.StartTimeUtc.ToString("u"));
        AddDetailRow(table, nameof(TraceRecord.DurationMs), record.DurationMs?.ToString("0.#"));
        AddDetailRow(table, nameof(TraceRecord.Success), record.Success?.ToString());
        AddDetailRow(table, nameof(TraceRecord.Severity), record.Severity);
        AddDetailRow(table, nameof(TraceRecord.CloudRoleName), record.CloudRoleName);
        AddDetailRow(table, nameof(TraceRecord.RequestType), record.RequestType);
        AddDetailRow(table, nameof(TraceRecord.ErrorId), record.ErrorId);
        AddDetailRow(table, nameof(TraceRecord.ErrorCategory), record.ErrorCategory);
        AddDetailRow(table, nameof(TraceRecord.HttpStatusCode), record.HttpStatusCode?.ToString());

        foreach (var detail in (record.Details ?? new Dictionary<string, string>())
                     .OrderBy(detail => detail.Key, StringComparer.OrdinalIgnoreCase))
        {
            AddDetailRow(table, detail.Key, detail.Value);
        }

        AnsiConsole.Write(new Rule($"[grey]Record #{recordNumber}[/]").LeftJustified());
        AnsiConsole.Write(table);
    }

    private static void AddDetailRow(Table table, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        table.AddRow(Markup.Escape(field), Markup.Escape(value));
    }

    private sealed record NumberedTraceRecord(int Number, TraceRecord Record);
}
