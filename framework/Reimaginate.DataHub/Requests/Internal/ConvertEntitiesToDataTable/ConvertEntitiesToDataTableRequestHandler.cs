using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ConvertEntitiesToDataTable;

public class ConvertEntitiesToDataTableRequestHandler : IHandler<ConvertEntitiesToDataTableRequest, DataTable>
{
    public Task<DataTable> HandleAsync(ConvertEntitiesToDataTableRequest request, CancellationToken cancellationToken)
    {

        var dt = new DataTable(request.TableName);
        foreach (var column in request.Columns)
        {
            dt.Columns.Add(column, typeof(string));
        }

        foreach (var entity in request.Entities)
        {
            var newRow = dt.NewRow();
            foreach (var column in request.Columns)
            {
                var value = entity[column];
                switch (value)
                {
                    case { } dateValue when dateValue.TryAsDateTimeOffset(out var dateTimeOffset):
                        newRow[column] = dateTimeOffset.ToString("o");
                        break;

                    default:
                        newRow[column] = value?.Value<string>() ?? string.Empty;
                        break;
                };
            }
            dt.Rows.Add(newRow);
        }

        return Task.FromResult(dt);
    }
}
