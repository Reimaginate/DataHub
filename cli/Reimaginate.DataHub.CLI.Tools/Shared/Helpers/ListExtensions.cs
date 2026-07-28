using System.Data;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Helpers;

public static class ListExtensions
{
    
    public static DataTable ToDataTable<T>(this List<T> items, List<string>? displayProps = null)
    {
        var dataTable = new DataTable(typeof(T).Name);

        if (typeof(T) == typeof(JObject))
        {
            var objectProps = items.OfType<JObject>().SelectMany(s => s.Properties()).Select(s => s.Name).Distinct().ToList();
            displayProps ??= objectProps;

            foreach (var prop in displayProps)
            {
                var col = new DataColumn(prop) { DataType = typeof(string) };
                dataTable.Columns.Add(col);
            }

            foreach (var item in items)
            {
                var values = new List<object>();

                var o = item as JObject;

                foreach (var prop in displayProps)
                {
                    var m = o?.SelectToken(prop);

                    if (m == null)
                    {
                        values.Add(DBNull.Value);
                        continue;
                    }

                    if (m.Type == JTokenType.Array)
                    {
                        values.Add(m.Value<JArray>()?.ToString() ?? string.Empty);
                    }
                    else 
                    {
                        values.Add(m.Value<string>() ?? string.Empty);
                    }

                }

                dataTable.Rows.Add(values.ToArray());
            }
        }


        if (typeof(T) != typeof(JObject))
        {
            var typeProps = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            displayProps ??= typeProps.OrderBy(o => o.Name).Select(s => s.Name).ToList();

            foreach (var prop in displayProps)
            {
                var propType = typeProps.First(f => f.Name == prop);

                var dataType = Nullable.GetUnderlyingType(propType.PropertyType) != null ? Nullable.GetUnderlyingType(propType.PropertyType) : propType.PropertyType;
                if (dataType == typeof(DateTimeOffset?))
                {
                    dataType = typeof(DateTime?);
                }

                if (dataType == typeof(DateTimeOffset))
                {
                    dataType = typeof(DateTime);
                }

                if (dataType == typeof(Exception) || dataType == typeof(JObject))
                {
                    dataType = typeof(string);
                }

                var col = new DataColumn(prop) { DataType = dataType };
                dataTable.Columns.Add(col);
            }

            foreach (var item in items)
            {
                var values = new List<object>();

                foreach (var prop in displayProps)
                {
                    var p = typeProps.First(f => f.Name == prop);
                    var val = typeProps.First(f => f.Name == prop).GetValue(item, null);

                    if (p.PropertyType == typeof(DateTimeOffset))
                    {
                        DateTime? dtVal = ((DateTimeOffset)val!).DateTime;
                        if (dtVal?.Year < 1900) dtVal = null;
                        values.Add(dtVal!);
                    }
                    else if (val?.GetType() == typeof(JObject))
                    {
                        values.Add(val.ToString()!);
                    }
                    else
                        values.Add(val ?? DBNull.Value);
                }

                dataTable.Rows.Add(values.ToArray());
            }
        }

        return dataTable;
    }
}
