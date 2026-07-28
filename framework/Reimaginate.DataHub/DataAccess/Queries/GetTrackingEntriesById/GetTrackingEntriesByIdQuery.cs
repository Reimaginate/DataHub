using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesById;

public class GetTrackingEntriesByIdQuery : IRequest<PagedResults<ChangeTrackingEntry>>
{
    public List<string> Ids { get; set; }
    public int? PageSize { get; set; }
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; } = false;
}