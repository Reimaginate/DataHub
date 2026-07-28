using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DetachEntitiesFromDataSource;

public class DetachEntitiesFromDataSourceRequest : IRequest<DetachEntitiesFromDataSourceResponse>
{
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; } = new();
    public string DataSource { get; set; }
}