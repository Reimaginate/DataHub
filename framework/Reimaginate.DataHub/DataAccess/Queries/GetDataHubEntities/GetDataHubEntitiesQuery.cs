using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;

public class GetDataHubEntitiesQuery : IRequest<PagedResults<JObject>>
{
    public string WhereClause { get; set; }
    public string Select { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
    public string OrderBy { get; set; }
    public bool GetTotalResultCount { get; set; } = false;
    public string From { get; set; } = "x";
    public IReadOnlyCollection<QueryParameter> Parameters { get; set; }
}
