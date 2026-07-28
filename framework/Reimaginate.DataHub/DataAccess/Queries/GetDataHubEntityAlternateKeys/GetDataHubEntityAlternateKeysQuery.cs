using Newtonsoft.Json.Linq;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntityAlternateKeys;

public class GetDataHubEntityAlternateKeysQuery : IRequest<PagedResults<JObject>>
{
    public string WhereClause { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
    public string OrderBy { get; set; }
    public bool GetTotalResultCount { get; set; } = false;

}