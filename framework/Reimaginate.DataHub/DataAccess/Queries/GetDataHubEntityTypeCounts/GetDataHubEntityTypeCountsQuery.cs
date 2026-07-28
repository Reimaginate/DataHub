using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntityTypeCounts;

public class GetDataHubEntityTypeCountsQuery : IRequest<List<EntityTypeCount>>
{
    public string WhereClause { get; set; }
    public string From { get; set; } = "x";
    public IReadOnlyCollection<QueryParameter> Parameters { get; set; }
}
