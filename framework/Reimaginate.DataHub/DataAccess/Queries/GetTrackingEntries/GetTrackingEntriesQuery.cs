using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;

public class GetTrackingEntriesQuery : IRequest<PagedResults<ChangeTrackingEntry>>
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public int? PageSize { get; set; }
    public DateTimeOffset? FromDateTime { get; set; }
    public DateTimeOffset? ToDateTime { get; set; }
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; } = false;
}