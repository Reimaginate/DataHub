using System.Collections.Generic;
using System.Data;
using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ConvertEntitiesToDataTable;

public class ConvertEntitiesToDataTableRequest : IRequest<DataTable>
{
    public List<string> Columns { get; set; }
    public List<JObject> Entities { get; set; }
    public string TableName { get; set; }
}