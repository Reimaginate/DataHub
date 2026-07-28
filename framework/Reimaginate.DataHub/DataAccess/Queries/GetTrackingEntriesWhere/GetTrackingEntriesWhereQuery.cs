using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesWhere;

public class GetTrackingEntriesWhereQuery : IRequest<PagedResults<ChangeTrackingEntry>>
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
