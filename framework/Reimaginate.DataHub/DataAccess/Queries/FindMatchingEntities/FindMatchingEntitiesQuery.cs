using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.FindMatchingEntities;

public sealed class FindMatchingEntitiesQuery : IRequest<JArray>
{
    public string EntityType { get; set; }
    public string WhereClause { get; set; }
    public string SelectClause { get; set; }
    public IReadOnlyCollection<QueryParameter> Parameters { get; set; }
}
