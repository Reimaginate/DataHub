using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Helpers;

public static class ConsoleHelper
{
    public static string GetCellValue<T>(T row, string path)
    {
        if (row != null)
        {
            var o = typeof(T) == typeof(JObject) ? row as JObject : JObject.FromObject(row);
            if (o == null) return string.Empty;

            var selectPath = path;
            if (selectPath.Contains("["))
            {
                var parts = selectPath.Split("[");
                selectPath = $"$.{parts[0]}[{parts[1]}";
            }

            var jVal = o.SelectToken(selectPath);
            if (jVal == null || jVal.Type == JTokenType.Null) return string.Empty;

            var val = jVal.ToString(Formatting.None).EscapeMarkup().Trim("\"".ToCharArray());
            return val;
        }

        return string.Empty;
    }

    public static void PrintTable<T>(List<T> data, List<string> columns)
    {
        data ??= [];
        columns ??= [];

        if (CliOutputContext.Format != CliOutputFormat.Table)
        {
            PrintStructured(data, columns);
            return;
        }

        var table = new Table();

        foreach (var column in columns)
        {
            var colName = column;
            if (colName.Contains("["))
            {
                colName = colName.Split("[")[0];
            }

            table.AddColumn(new TableColumn(colName.EscapeMarkup()));
        }

        foreach (var row in data)
        {
            var cellValues = columns.Select(s => GetCellValue(row, s)).ToArray();
            table.AddRow(cellValues);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void PrintStructured<T>(List<T> data, List<string> columns)
    {
        switch (CliOutputContext.Format)
        {
            case CliOutputFormat.Json:
                Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));
                break;

            case CliOutputFormat.Ndjson:
                foreach (var row in data)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(row, Formatting.None));
                }
                break;

            case CliOutputFormat.Tsv:
                Console.WriteLine(string.Join('\t', columns));
                foreach (var row in data)
                {
                    Console.WriteLine(string.Join('\t', columns.Select(column => GetCellValue(row, column))));
                }
                break;
        }
    }
}
