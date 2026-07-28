using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetLogs;

public class GetLogsRequest<TSyncEvent> : IRequest<GetLogsResponse> where TSyncEvent : SyncEvent
{
    public string WhereClause { get; set; }

    public string OrderBy { get; set; }

    public int PageSize { get; set; } = 100;

    public string ContinuationToken { get; set; }

    public bool RetrieveAllResults { get; set; } = false;

    public IReadOnlyCollection<QueryParameter> Parameters { get; set; }
}
