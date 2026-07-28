using ClosedXML.Excel;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Helpers;

public static class ExcelExtension
{
    public static string? GetColumn(this IXLWorksheet worksheet, string columnName)
    {
        var cell = worksheet.FirstRowUsed()?.CellsUsed(c => c.Value.ToString().ToLower() == columnName.ToLower()).FirstOrDefault();
        if (cell != null)
        {
            return cell.WorksheetColumn().ColumnLetter();
        }
        return null;
    }
}
