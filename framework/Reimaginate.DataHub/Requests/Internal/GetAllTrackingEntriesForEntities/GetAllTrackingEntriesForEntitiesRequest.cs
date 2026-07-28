using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;

public class GetAllTrackingEntriesForEntitiesRequest : IRequest<GetAllTrackingEntriesForEntitiesResponse>
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
}