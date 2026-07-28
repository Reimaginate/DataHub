using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json.Linq;
using Humanizer;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Helpers;

public static class CsvExportHelper
{
    public static void WriteObjectsToCsv<T>(string path, List<T> records, List<string> props, string delimiter = ",", bool humanize = false)
    {
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter
        };
        if (typeof(T) == typeof(JObject))
        {
            using (var fileStream = new StreamWriter(path))
            using (var csvStream = new CsvWriter(fileStream, csvConfig))
            {
                foreach (var prop in props)
                {
                    csvStream.WriteField(prop);
                }
                csvStream.NextRecord();

                foreach (var record in (records as List<JObject>)!)
                {
                    foreach (var prop in props)
                    {
                        if (humanize)
                        {
                            csvStream.WriteField(record.Value<string>(prop)?.Humanize());
                        }
                        else
                        {
                            csvStream.WriteField(record.Value<string>(prop));
                        }
                    }
                    csvStream.NextRecord();
                }

                csvStream.Flush();
            }

                
        }
        else
        {
            using (var fileStream = new StreamWriter(path))
            using (var csvStream = new CsvWriter(fileStream, csvConfig))
            {
                foreach (var prop in props)
                {
                    csvStream.WriteField(prop);
                }
                csvStream.NextRecord();

                foreach (var record in records)
                {
                    foreach (var prop in props)
                    {
                        if (humanize)
                        {
                            csvStream.WriteField(typeof(T).GetProperty(prop)?.GetValue(record)?.ToString()?.Humanize());
                        }
                        else
                        {
                            csvStream.WriteField(typeof(T).GetProperty(prop)?.GetValue(record)?.ToString());
                        }
                    }
                    csvStream.NextRecord();
                }

                csvStream.Flush();
            }
        }
    }

    public static void WriteObjectsToExcel<T>(string file, List<T> results, List<string> props, bool humanize = false)
    {
        if (typeof(T) == typeof(JObject))
        {
            using (var excelWorkbook = new XLWorkbook())
            {
                var dataSheet = excelWorkbook.Worksheets.Add(0);
                for (int i = 0; i < props.Count; i++)
                {
                    dataSheet.Cell(1, i + 1).Value = props[i];
                }

                for (int i = 0; i < results.Count; i++)
                {
                    for (int j = 0; j < props.Count; j++)
                    {
                        if (humanize)
                        {
                            dataSheet.Cell(i + 2, j + 1).Value = (results[i] as JObject)!.Value<string>(props[j])?.Humanize();
                        }
                        else
                        {
                            dataSheet.Cell(i + 2, j + 1).Value = (results[i] as JObject)!.Value<string>(props[j]);
                        }
                    }
                }

                excelWorkbook.SaveAs(file);
            }
        }
        else
        {
            using (var excelWorkbook = new XLWorkbook())
            {
                var dataSheet = excelWorkbook.Worksheets.Add(0);
                for (int i = 0; i < props.Count; i++)
                {
                    dataSheet.Cell(1, i + 1).Value = props[i];
                }

                for (int i = 0; i < results.Count; i++)
                {
                    for (int j = 0; j < props.Count; j++)
                    {
                        if (humanize)
                        {
                            dataSheet.Cell(i + 2, j + 1).Value = typeof(T).GetProperty(props[j])?.GetValue(results[i])?.ToString()?.Humanize();
                        }
                        else
                        {
                            dataSheet.Cell(i + 2, j + 1).Value = typeof(T).GetProperty(props[j])?.GetValue(results[i])?.ToString();
                        }
                    }
                }

                excelWorkbook.SaveAs(file);
            }
        }
    }
}
